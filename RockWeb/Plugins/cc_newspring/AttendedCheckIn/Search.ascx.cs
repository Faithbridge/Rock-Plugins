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
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.UI;
using Rock;
using Rock.Attribute;
using Rock.CheckIn;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.cc_newspring.AttendedCheckin
{
    /// <summary>
    /// Search block for Attended Check-in
    /// </summary>
    [DisplayName( "Search Block" )]
    [Category( "Check-in > Attended" )]
    [Description( "Attended Check-In Search block" )]
    [LinkedPage( "Admin Page" )]
    [BooleanField( "Show Key Pad", "Show the number key pad on the search screen", false )]
    [IntegerField( "Minimum Text Length", "Minimum length for text searches (defaults to 1).", false, 1 )]
    [IntegerField( "Maximum Text Length", "Maximum length for text searches (defaults to 20).", false, 20 )]
    [TextField( "Search Regex", "Regular Expression to run the search input through before sending it to the workflow. Useful for stripping off characters.", false )]
    public partial class Search : CheckInBlock
    {
        #region Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            if ( CurrentCheckInState == null )
            {
                NavigateToLinkedPage( "AdminPage" );
                return;
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack && CurrentCheckInState != null )
            {
                if ( !string.IsNullOrWhiteSpace( CurrentCheckInState.CheckIn.SearchValue ) )
                {
                    tbSearchBox.Text = CurrentCheckInState.CheckIn.SearchValue;
                }

                string script = string.Format( @"
            <script>
                $(document).ready(function (e) {{
                    if (localStorage) {{
                        localStorage.checkInKiosk = '{0}';
                        localStorage.checkInGroupTypes = '{1}';
                    }}
                }});
            </script>
            ", CurrentKioskId, CurrentGroupTypeIds.AsDelimited( "," ) );
                phScript.Controls.Add( new LiteralControl( script ) );

                if ( GetAttributeValue( "ShowKeyPad" ).AsBoolean() )
                {
                    pnlKeyPad.Visible = true;
                }

                tbSearchBox.Focus();
            }

        }

        #endregion

        #region Click Events

        /// <summary>
        /// Handles the Click event of the lbSearch control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbSearch_Click( object sender, EventArgs e )
        {
            CurrentCheckInState.CheckIn.Families.Clear();
            CurrentCheckInState.CheckIn.UserEnteredSearch = true;
            CurrentCheckInState.CheckIn.ConfirmSingleFamily = true;

            int minLength = int.Parse( GetAttributeValue( "MinimumTextLength" ) );
            int maxLength = int.Parse( GetAttributeValue( "MaximumTextLength" ) );
            if ( tbSearchBox.Text.Length >= minLength && tbSearchBox.Text.Length <= maxLength )
            {
                string searchInput = tbSearchBox.Text;

                // run regex expression on input if provided
                if ( !string.IsNullOrWhiteSpace( GetAttributeValue( "SearchRegex" ) ) )
                {
                    Regex regex = new Regex( GetAttributeValue( "SearchRegex" ) );
                    Match match = regex.Match( searchInput );
                    if ( match.Success )
                    {
                        if ( match.Groups.Count == 2 )
                        {
                            searchInput = match.Groups[1].ToString();
                        }
                    }
                }

                double searchNumber;
                if ( Double.TryParse( searchInput, out searchNumber ) )
                {
                    CurrentCheckInState.CheckIn.SearchType = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.CHECKIN_SEARCH_TYPE_PHONE_NUMBER );
                }
                else
                {
                    CurrentCheckInState.CheckIn.SearchType = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.CHECKIN_SEARCH_TYPE_NAME );
                }

                // remember the current search value
                CurrentCheckInState.CheckIn.SearchValue = searchInput;

                var errors = new List<string>();
                if ( ProcessActivity( "Family Search", out errors ) )
                {
                    SaveState();
                    NavigateToNextPage();
                }
                else
                {
                    string errorMsg = "<ul><li>" + errors.AsDelimited( "</li><li>" ) + "</li></ul>";
                    maWarning.Show( errorMsg.Replace( "'", @"\'" ), ModalAlertType.Warning );
                }
            }
            else
            {
                string errorMsg = ( tbSearchBox.Text.Length > maxLength )
                    ? string.Format( "<ul><li>Please enter no more than {0} character(s)</li></ul>", maxLength )
                    : string.Format( "<ul><li>Please enter at least {0} character(s)</li></ul>", minLength );

                maWarning.Show( errorMsg, ModalAlertType.Warning );
            }
        }

        /// <summary>
        /// Handles the Click event of the lbBack control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbBack_Click( object sender, EventArgs e )
        {
            bool hasFamilyCheckedIn = CurrentCheckInState.CheckIn.Families.Any( f => f.Selected );
            if ( !hasFamilyCheckedIn )
            {
                var queryParams = new Dictionary<string, string>();
                queryParams.Add( "back", "true" );
                NavigateToLinkedPage( "AdminPage", queryParams );
            }
            else
            {
                NavigateToPreviousPage();
            }
        }

        #endregion
    }
}