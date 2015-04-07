﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Rock;
using Rock.Data;
using Rock.Financial;
using Rock.Model;
using Rock.Rest;
using Rock.Rest.Filters;
using Rock.Web.Cache;

namespace cc.newspring.Apollos.Rest.Controllers
{
    public class GiveController : ApiControllerBase
    {
        private static string gatewayName = "Rock.CyberSource.Gateway";

        /// <summary>
        /// Gives through CyberSource with the specified give parameters.
        /// </summary>
        /// <param name="giveParameters">The give parameters.</param>
        /// <returns></returns>
        [Authenticate, Secured]
        [HttpPost]
        [System.Web.Http.Route( "api/Give" )]
        public HttpResponseMessage Give( [FromBody]GiveParameters giveParameters )
        {
            // Validate required fields for all cases
            if ( string.IsNullOrWhiteSpace( giveParameters.PhoneNumber ) )
            {
                return GenerateResponse( HttpStatusCode.BadRequest, "PhoneNumber is required" );
            }

            if ( string.IsNullOrWhiteSpace( giveParameters.Email ) )
            {
                return GenerateResponse( HttpStatusCode.BadRequest, "Email is required" );
            }

            if ( string.IsNullOrWhiteSpace( giveParameters.FirstName ) ||
                string.IsNullOrWhiteSpace( giveParameters.LastName ) )
            {
                return GenerateResponse( HttpStatusCode.BadRequest, "FirstName and LastName are required" );
            }

            if ( string.IsNullOrWhiteSpace( giveParameters.Street1 ) ||
                            string.IsNullOrWhiteSpace( giveParameters.City ) ||
                            string.IsNullOrWhiteSpace( giveParameters.State ) ||
                            string.IsNullOrWhiteSpace( giveParameters.PostalCode ) )
            {
                return GenerateResponse( HttpStatusCode.BadRequest, "Street1, City, State, and PostalCode are required for credit transactions" );
            }

            if ( giveParameters.State.Length != 2 )
            {
                return GenerateResponse( HttpStatusCode.BadRequest, "State must be a 2 letter string" );
            }

            if ( giveParameters.SourceAccountId.HasValue )
            {
                // Validate saved account case
                if ( !giveParameters.PersonId.HasValue )
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "PersonId is required to use an existing account (SourceAccountId)" );
                }
            }
            else
            {
                // No existing account case
                if ( string.IsNullOrWhiteSpace( giveParameters.AccountNumber ) && !giveParameters.SourceAccountId.HasValue )
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "AccountNumber is required" );
                }

                if ( string.IsNullOrWhiteSpace( giveParameters.AccountType ) && !giveParameters.SourceAccountId.HasValue )
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "AccountType is required and must be one of checking, savings, or credit" );
                }

                switch ( giveParameters.AccountType.ToLower() )
                {
                    case "checking":
                    case "savings":
                        if ( string.IsNullOrWhiteSpace( giveParameters.RoutingNumber ) )
                        {
                            return GenerateResponse( HttpStatusCode.BadRequest, "RoutingNumber is required for ACH transactions" );
                        }
                        break;

                    case "credit":
                        if ( string.IsNullOrWhiteSpace( giveParameters.CCV ) )
                        {
                            return GenerateResponse( HttpStatusCode.BadRequest, "CCV is required for credit transactions" );
                        }
                        if ( giveParameters.ExpirationMonth < 1 || giveParameters.ExpirationMonth > 12 )
                        {
                            return GenerateResponse( HttpStatusCode.BadRequest, "ExpirationMonth is required and must be between 1 and 12 for credit transactions" );
                        }
                        var currentDate = DateTime.Now;
                        var maxYear = currentDate.Year + 30;
                        if ( giveParameters.ExpirationYear < currentDate.Year || giveParameters.ExpirationYear > maxYear )
                        {
                            return GenerateResponse( HttpStatusCode.BadRequest, string.Format( "ExpirationYear is required and must be between {0} and {1} for credit transactions", currentDate.Year, maxYear ) );
                        }
                        if ( giveParameters.ExpirationYear <= currentDate.Year && giveParameters.ExpirationMonth < currentDate.Month )
                        {
                            return GenerateResponse( HttpStatusCode.BadRequest, "The ExpirationMonth and ExpirationYear combination must not have already elapsed for credit transactions" );
                        }

                        break;

                    default:
                        return GenerateResponse( HttpStatusCode.BadRequest, "AccountType is required and must be one of checking, savings, or credit" );
                }
            }

            if ( giveParameters.AmountDetails == null || giveParameters.AmountDetails.Length == 0 )
            {
                return GenerateResponse( HttpStatusCode.BadRequest, "AmountDetails are required and the sum of them must be greater than or equal to 1" );
            }

            var totalAmount = 0m;
            var rockContext = new RockContext();

            foreach ( var accountAmount in giveParameters.AmountDetails )
            {
                if ( accountAmount.Amount < 1m )
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "AmountDetails/Amount is required and must be greater than or equal to 1" );
                }
                if ( accountAmount.TargetAccountId == 0 )
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "AmountDetails/TargetAccountId is required" );
                }
                if ( new FinancialAccountService( rockContext ).Get( accountAmount.TargetAccountId ) == null )
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "AmountDetails/TargetAccountId must be an existing account's id" );
                }

                totalAmount += accountAmount.Amount;
            }

            var gateway = GatewayContainer.GetComponent( gatewayName );

            if ( gateway == null )
            {
                return GenerateResponse( HttpStatusCode.InternalServerError, "There was a problem creating the payment gateway information" );
            }

            Person person = null;

            if ( giveParameters.PersonId.HasValue )
            {
                person = new PersonService( rockContext ).Get( giveParameters.PersonId.Value );

                if ( person == null )
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "The PersonId did not resolve to an existing person record" );
                }
            }
            else
            {
                person = CreatePerson( giveParameters );

                if ( person == null )
                {
                    return GenerateResponse( HttpStatusCode.InternalServerError, "There was a problem creating a person with the supplied details" );
                }
            }

            PaymentInfo paymentInfo = null;

            if ( giveParameters.SourceAccountId.HasValue )
            {
                var account = new FinancialPersonSavedAccountService( rockContext ).Get( giveParameters.SourceAccountId.Value );

                if ( account != null && account.PersonAliasId.HasValue && account.PersonAliasId == person.PrimaryAliasId )
                {
                    paymentInfo = account.GetReferencePayment();
                }
                else
                {
                    return GenerateResponse( HttpStatusCode.BadRequest, "The SourceAccountId did not resolve to a saved account's id" );
                }
            }
            else
            {
                if ( giveParameters.AccountType.ToLower() == "credit" )
                {
                    paymentInfo = new CreditCardPaymentInfo()
                    {
                        Number = giveParameters.AccountNumber,
                        Code = giveParameters.CCV,
                        ExpirationDate = new DateTime( giveParameters.ExpirationYear, giveParameters.ExpirationMonth, 1 ),
                        BillingStreet1 = giveParameters.Street1,
                        BillingStreet2 = giveParameters.Street2,
                        BillingCity = giveParameters.City,
                        BillingState = giveParameters.State,
                        BillingPostalCode = giveParameters.PostalCode,
                        BillingCountry = giveParameters.Country
                    };
                }
                else
                {
                    paymentInfo = new ACHPaymentInfo()
                    {
                        BankRoutingNumber = giveParameters.RoutingNumber,
                        BankAccountNumber = giveParameters.AccountNumber,
                        AccountType = giveParameters.AccountType.ToLower() == "checking" ? BankAccountType.Checking : BankAccountType.Savings
                    };
                }
            }

            paymentInfo.Amount = totalAmount;
            paymentInfo.FirstName = giveParameters.FirstName;
            paymentInfo.LastName = giveParameters.LastName;
            paymentInfo.Email = giveParameters.Email;
            paymentInfo.Phone = giveParameters.PhoneNumber;
            paymentInfo.Street1 = giveParameters.Street1;
            paymentInfo.Street2 = giveParameters.Street2;
            paymentInfo.City = giveParameters.City;
            paymentInfo.State = giveParameters.State;
            paymentInfo.PostalCode = giveParameters.PostalCode;
            paymentInfo.Country = giveParameters.Country;

            string errorMessage;
            var transaction = gateway.Charge( paymentInfo, out errorMessage );

            if ( transaction == null || !string.IsNullOrWhiteSpace( errorMessage ) )
            {
                return GenerateResponse( HttpStatusCode.InternalServerError, errorMessage ?? "The gateway had a problem and/or did not create a transaction as expected" );
            }

            transaction.TransactionDateTime = RockDateTime.Now;
            transaction.AuthorizedPersonAliasId = person.PrimaryAliasId;
            transaction.GatewayEntityTypeId = gateway.TypeId;
            transaction.TransactionTypeValueId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION ) ).Id;
            transaction.CurrencyTypeValueId = paymentInfo.CurrencyTypeValue.Id;

            if ( paymentInfo.CreditCardTypeValue != null )
            {
                transaction.CreditCardTypeValueId = paymentInfo.CreditCardTypeValue.Id;
            }

            foreach ( var accountAmount in giveParameters.AmountDetails )
            {
                transaction.TransactionDetails.Add( new FinancialTransactionDetail()
                {
                    Amount = accountAmount.Amount,
                    AccountId = accountAmount.TargetAccountId
                } );
            }

            new FinancialTransactionService( rockContext ).Add( transaction );

            if ( !giveParameters.SourceAccountId.HasValue )
            {
                // Get a reference number to allow "saving" this account for future use (don't throw errors here because the gateway already received the payment request)
                var newReferenceNumber = gateway.GetReferenceNumber( transaction, out errorMessage );

                // If we got a reference number and we can reference the person, save the account - skip if there was an error getting the reference number
                if ( person.PrimaryAliasId.HasValue && !string.IsNullOrWhiteSpace( newReferenceNumber ) && string.IsNullOrWhiteSpace( errorMessage ) )
                {
                    var savedAccountService = new FinancialPersonSavedAccountService( rockContext );
                    var maskedAccountNumber = Mask( giveParameters.AccountNumber );

                    // Check for an account belonging to this person with the same mask that already has a reference
                    var savedAccount = savedAccountService.Queryable().Where( a =>
                        a.PersonAliasId == person.PrimaryAliasId.Value &&
                        a.MaskedAccountNumber == maskedAccountNumber &&
                        a.ReferenceNumber != null ).FirstOrDefault();

                    // If that account does not exist, save this account for future giving ease
                    if ( savedAccount == null )
                    {
                        savedAccount = new FinancialPersonSavedAccount();
                        savedAccount.PersonAliasId = person.PrimaryAliasId;
                        savedAccount.MaskedAccountNumber = maskedAccountNumber;
                        savedAccount.TransactionCode = transaction.TransactionCode;
                        savedAccount.GatewayEntityTypeId = gateway.TypeId;
                        savedAccount.CurrencyTypeValueId = transaction.CurrencyTypeValueId;
                        savedAccount.CreditCardTypeValueId = transaction.CreditCardTypeValueId;
                        savedAccount.ReferenceNumber = newReferenceNumber;

                        if ( paymentInfo.CreditCardTypeValue != null )
                        {
                            savedAccount.Name = paymentInfo.CreditCardTypeValue.Description;
                        }
                        else
                        {
                            var name = giveParameters.AccountType;
                            savedAccount.Name = char.ToUpper( name[0] ) + name.Substring( 1 );
                        }

                        savedAccountService.Add( savedAccount );
                    }

                    // If this is a bank account, save it to the bank account service for check scanning functionality
                    if ( giveParameters.AccountType.ToLower() == "checking" || giveParameters.AccountType.ToLower() == "savings" )
                    {
                        var bankAccountService = new FinancialPersonBankAccountService( rockContext );
                        var bankAccount = bankAccountService.Get( giveParameters.RoutingNumber, giveParameters.AccountNumber );

                        if ( bankAccount == null || bankAccount.PersonAliasId != person.PrimaryAliasId.Value )
                        {
                            bankAccount = new FinancialPersonBankAccount();
                            bankAccount.PersonAliasId = person.PrimaryAliasId.Value;
                            bankAccount.AccountNumberMasked = maskedAccountNumber;
                            bankAccount.AccountNumberSecured = FinancialPersonBankAccount.EncodeAccountNumber( giveParameters.RoutingNumber, giveParameters.AccountNumber );
                            bankAccountService.Add( bankAccount );
                        }
                    }
                }
            }

            rockContext.SaveChanges();
            return GenerateResponse( HttpStatusCode.NoContent );
        }

        /// <summary>
        /// Creates a person within his/her own family using the giving parameters.
        /// </summary>
        /// <param name="giveParameters">The give parameters.</param>
        /// <returns></returns>
        private Person CreatePerson( GiveParameters giveParameters )
        {
            var person = new Person()
            {
                FirstName = giveParameters.FirstName,
                LastName = giveParameters.LastName
            };

            if ( !string.IsNullOrWhiteSpace( giveParameters.PhoneNumber ) )
            {
                person.PhoneNumbers.Add( new PhoneNumber()
                {
                    Number = giveParameters.PhoneNumber,
                    NumberTypeValueId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME ) ).Id
                } );
            }

            var rockContext = new RockContext();
            new PersonService( rockContext ).Add( person );

            // Need to save to get the person's Id
            rockContext.SaveChanges();

            if ( !person.Aliases.Any() )
            {
                person.Aliases.Add( new PersonAlias { AliasPersonId = person.Id, AliasPersonGuid = person.Guid } );
            }

            var familyGroupType = GroupTypeCache.GetFamilyGroupType();
            var adultId = familyGroupType.Roles.FirstOrDefault( r =>
                r.Guid == new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;

            var familyGroup = new Group
            {
                GroupTypeId = familyGroupType.Id,
                IsSecurityRole = false,
                IsSystem = false,
                IsActive = true,
                Name = person.LastName + " Family"
            };

            if ( !( string.IsNullOrWhiteSpace( giveParameters.Street1 ) ||
                        string.IsNullOrWhiteSpace( giveParameters.City ) ||
                        string.IsNullOrWhiteSpace( giveParameters.State ) ||
                        string.IsNullOrWhiteSpace( giveParameters.PostalCode ) ) )
            {
                familyGroup.GroupLocations.Add( new GroupLocation()
                {
                    GroupLocationTypeValueId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME ) ).Id,
                    Location = new Location()
                    {
                        Street1 = giveParameters.Street1,
                        Street2 = giveParameters.Street2,
                        City = giveParameters.City,
                        State = giveParameters.State,
                        PostalCode = giveParameters.PostalCode,
                        Country = giveParameters.Country
                    }
                } );
            }

            new GroupService( rockContext ).Add( familyGroup );

            var groupMember = new GroupMember
            {
                IsSystem = false,
                GroupId = familyGroup.Id,
                PersonId = person.Id,
                GroupRoleId = adultId
            };

            familyGroup.Members.Add( groupMember );
            rockContext.SaveChanges();

            return person;
        }

        /// <summary>
        /// Generates a response for an API request.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        private HttpResponseMessage GenerateResponse( HttpStatusCode code, string message = null )
        {
            var response = new HttpResponseMessage( code );

            if ( !string.IsNullOrWhiteSpace( message ) )
            {
                response.Content = new StringContent( message );
            }

            return response;
        }

        /// <summary>
        /// Masks the specified string to look something like ************1234.
        /// </summary>
        /// <param name="unmasked">The unmasked string.</param>
        /// <param name="charsToShow">The number of chars to show.</param>
        /// <param name="maskChar">The mask character.</param>
        /// <returns></returns>
        private string Mask( string unmasked, int charsToShow = 4, char maskChar = '*' )
        {
            var lengthOfUnmasked = unmasked.Length;
            var charsToMask = lengthOfUnmasked - charsToShow;

            if ( lengthOfUnmasked <= charsToShow )
            {
                return unmasked;
            }

            var mask = string.Empty.PadLeft( charsToMask, maskChar );
            var shown = unmasked.Substring( unmasked.Length - charsToShow );
            return string.Concat( mask, shown );
        }
    }
}