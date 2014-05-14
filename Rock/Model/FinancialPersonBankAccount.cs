﻿// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Rock.Data;

namespace Rock.Model
{
    /// <summary>
    /// Represents a relationship between a person and a bank account in Rock. A person can be related to multiple bank accounts
    /// but a bank account can only be related to an individual person in Rock.
    /// </summary>
    [Table( "FinancialPersonBankAccount" )]
    [DataContract]
    public partial class FinancialPersonBankAccount : Model<FinancialPersonBankAccount>
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the PersonId of the <see cref="Rock.Model.Person"/> who owns the account.
        /// </summary>
        /// <value>
        /// A <see cref="System.Int32"/> representing the PersonId of the <see cref="Rock.Model.Person"/> who owns the account.
        /// </value>
        [DataMember]
        public int PersonId { get; set; }

        /// <summary>
        /// Gets or sets hash of the Checking Account AccountNumber.  Stored as a SHA1 hash so that it can be matched without being known
        /// Enables a match of a Check Account to Person ( or Persons if multiple persons share a checking account) can be made
        /// </summary>
        /// <value>
        /// AccountNumberSecured.
        /// </value>
        [Required]
        [MaxLength( 128 )]
        public string AccountNumberSecured { get; set; }

        #endregion

        #region Virtual Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Person"/> who owns the account.
        /// </summary>
        /// <value>
        /// The <see cref="Rock.Model.Person"/> who owns the account.
        /// </value>
        public virtual Person Person { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return this.AccountNumberSecured.ToStringSafe();
        }

        /// <summary>
        /// Encodes the account number.
        /// </summary>
        /// <param name="routingNumber">The routing number.</param>
        /// <param name="accountNumber">The account number.</param>
        /// <returns></returns>
        /// <exception cref="System.Configuration.ConfigurationErrorsException">Account encoding requires a 'PasswordKey' app setting</exception>
        public static string EncodeAccountNumber( string routingNumber, string accountNumber )
        {
            var passwordKey = ConfigurationManager.AppSettings["PasswordKey"];
            if ( String.IsNullOrWhiteSpace( passwordKey ) )
            {
                throw new ConfigurationErrorsException( "Account encoding requires a 'PasswordKey' app setting" );
            }

            byte[] encryptionKey = HexToByte( passwordKey );

            HMACSHA1 hash = new HMACSHA1();
            hash.Key = encryptionKey;

            string toHash = string.Format( "{0}|{1}", routingNumber, accountNumber );
            return Convert.ToBase64String( hash.ComputeHash( Encoding.Unicode.GetBytes( toHash ) ) );
        }

        private static byte[] HexToByte( string hexString )
        {
            byte[] returnBytes = new byte[hexString.Length / 2];
            for ( int i = 0; i < returnBytes.Length; i++ )
                returnBytes[i] = Convert.ToByte( hexString.Substring( i * 2, 2 ), 16 );
            return returnBytes;
        }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// FinancialPersonBankAccount Configuration class.
    /// </summary>
    public partial class FinancialPersonBankAccountConfiguration : EntityTypeConfiguration<FinancialPersonBankAccount>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FinancialPersonBankAccountConfiguration"/> class.
        /// </summary>
        public FinancialPersonBankAccountConfiguration()
        {
            this.HasRequired( b => b.Person ).WithMany().HasForeignKey( b => b.PersonId ).WillCascadeOnDelete( true );
        }
    }

    #endregion
}