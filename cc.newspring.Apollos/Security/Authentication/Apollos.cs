﻿/* Apollos Authentication provider developed using BCrypt.Net:
     * http://bcrypt.codeplex.com/
     * Copyright (c) 2006, 2010, Damien Miller <djm@mindrot.org>, Ryan Emerle
     *
     * Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the
     * documentation and/or other materials provided with the distribution.
     * Neither the name of BCrypt.Net nor the names of its contributors may be used to endorse or promote products derived from this software without
     * specific prior written permission.
     *
     * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
     * TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
     * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
     * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
     * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
     * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
     */

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Security.Cryptography;
using System.Text;
using RestSharp;
using Rock;
using Rock.Attribute;
using Rock.Model;
using Rock.Security;

namespace cc.newspring.Apollos.Security.Authentication
{
    [Description( "Apollos Authentication Provider" )]
    [Export( typeof( AuthenticationComponent ) )]
    [ExportMetadata( "ComponentName", "Apollos" )]
    [IntegerField( "Work Factor", "Iteration count to generate salts in BCrypt", false, 10 )]
    [BooleanField( "Allow Change Password", "Set to true to allow user to change their password from the Rock system", true, "Server" )]
    public class Apollos : AuthenticationComponent
    {
        /// <summary>
        /// Initializes the <see cref="Database" /> class.
        /// </summary>
        static Apollos()
        {
        }

        /// <summary>
        /// Gets the type of the service.
        /// </summary>
        /// <value>
        /// The type of the service.
        /// </value>
        public override AuthenticationServiceType ServiceType
        {
            get { return AuthenticationServiceType.Internal; }
        }

        /// <summary>
        /// Determines if user is directed to another site (i.e. Facebook, Gmail, Twitter, etc) to confirm approval of using
        /// that site's credentials for authentication.
        /// </summary>
        /// <value>
        /// The requires remote authentication.
        /// </value>
        public override bool RequiresRemoteAuthentication
        {
            get { return false; }
        }

        /// <summary>
        /// Authenticates the specified user name and password
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        public override bool Authenticate( UserLogin user, string password )
        {
            return EncodePassword( user, password ) == user.Password;
        }

        /// <summary>
        /// Authenticates the user based on a request from a third-party provider.  Will set the username and returnUrl values.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="returnUrl">The return URL.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override bool Authenticate( System.Web.HttpRequest request, out string userName, out string returnUrl )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Encodes the password.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="password"></param>
        /// <returns></returns>
        public override string EncodePassword( UserLogin user, string password )
        {
            var workFactor = GetAttributeValue( "WorkFactor" ).AsType<int>();
            var hashedPassword = SHA256( password );
            var salt = user.Password;
            if ( string.IsNullOrEmpty( salt ) || !salt.StartsWith( "$2a$" ) )
            {
                salt = BCrypt.Net.BCrypt.GenerateSalt( workFactor );
            }

            var hash = BCrypt.Net.BCrypt.HashPassword( hashedPassword, salt );
            return hash;
        }

        /// <summary>
        /// Generates the login URL.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Uri GenerateLoginUrl( System.Web.HttpRequest request )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Tests the Http Request to determine if authentication should be tested by this
        /// authentication provider.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override bool IsReturningFromAuthentication( System.Web.HttpRequest request )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the URL of an image that should be displayed.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override string ImageUrl()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a value indicating whether [supports change password].
        /// </summary>
        /// <value>
        /// <c>true</c> if [supports change password]; otherwise, <c>false</c>.
        /// </value>
        public override bool SupportsChangePassword
        {
            get
            {
                return GetAttributeValue( "AllowChangePassword" ).AsType<Boolean>();
            }
        }

        /// <summary>
        /// Changes the password.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="oldPassword">The old password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <param name="warningMessage">The warning message.</param>
        /// <returns></returns>
        public override bool ChangePassword( UserLogin user, string oldPassword, string newPassword, out string warningMessage )
        {
            warningMessage = null;
            AuthenticationComponent authenticationComponent = AuthenticationContainer.GetComponent( user.EntityType.Name );

            if ( authenticationComponent == null || !authenticationComponent.IsActive )
            {
                throw new Exception( string.Format( "'{0}' service does not exist, or is not active", user.EntityType.FriendlyName ) );
            }

            if ( authenticationComponent.ServiceType == AuthenticationServiceType.External )
            {
                throw new Exception( "Cannot change password on external service type" );
            }

            if ( !authenticationComponent.Authenticate( user, oldPassword ) )
            {
                return false;
            }

            user.Password = authenticationComponent.EncodePassword( user, newPassword );
            user.LastPasswordChangedDateTime = RockDateTime.Now;

            return true;
        }

        /// <summary>
        /// SHA256s the specified password.
        /// </summary>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        private static string SHA256( string password )
        {
            SHA256Managed crypt = new SHA256Managed();
            string hash = String.Empty;
            byte[] crypto = crypt.ComputeHash( Encoding.UTF8.GetBytes( password ), 0, Encoding.UTF8.GetByteCount( password ) );

            foreach ( byte bit in crypto )
            {
                hash += bit.ToString( "x2" );
            }

            return hash;
        }
    }
}