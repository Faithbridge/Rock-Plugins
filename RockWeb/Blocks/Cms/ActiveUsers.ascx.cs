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
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Attribute;
using System.Text;
using System.Web;

namespace RockWeb.Blocks.Cms
{
    /// <summary>
    /// Template block for developers to use to start a new block.
    /// </summary>
    [DisplayName( "Active Users" )]
    [Category( "CMS" )]
    [Description( "Displays a list of active users of a website." )]
    [SiteField("Site", "Site to show current active users for.", true)]
    [BooleanField( "Show Site Name As Title", "Detmine whether to show the name of the site as a title above the list.", true )]
    [LinkedPage("Person Profile Page", "Page reference to the person profil page you would like to use as a link. Not providing a reference will suppress the creation of a link.", false)]
    [IntegerField("Page View Count", "The number of past page views to show on roll-over. A value of 0 will disable the roll-over.", true, 5)]
    public partial class ActiveUsers : Rock.Web.UI.RockBlock
    {
        #region Fields

        // used for private variables

        #endregion

        #region Properties

        // used for public / protected properties

        #endregion

        #region Base Control Methods
        
        //  overrides of the base RockBlock methods (i.e. OnInit, OnLoad)

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            ShowActiveUsers();
        }

        #endregion

        #region Events

        // handlers called by the controls on your block

        /// <summary>
        /// Handles the BlockUpdated event of the active users control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated(object sender, EventArgs e)
        {
            ShowActiveUsers();
        }

        #endregion

        #region Methods

       // helper functional methods (like BindGrid(), etc.)
        private void ShowActiveUsers()
        {
            if ( !string.IsNullOrEmpty( GetAttributeValue( "Site" ) ) )
            {
                int? pageViewCount = GetAttributeValue( "PageViewCount" ).AsIntegerOrNull();
                if ( !pageViewCount.HasValue || pageViewCount.Value == 0 )
                {
                    pageViewCount = 1;
                }

                StringBuilder sbUsers = new StringBuilder();

                var site = SiteCache.Read( GetAttributeValue( "Site" ).AsInteger() );
                lSiteName.Text = "<h4>" + site.Name + "</h4>";
                lSiteName.Visible = GetAttributeValue( "ShowSiteNameAsTitle" ).AsBoolean();

                lMessages.Text = string.Empty;

                var rockContext = new RockContext();

                IQueryable<PageView> pageViewQry = new PageViewService( rockContext ).Queryable( "Page" );

                // Query to get who is logged in and last visit was to selected site
                var activeLogins = new UserLoginService( rockContext ).Queryable( "Person" )
                    .Where( l =>
                        l.PersonId.HasValue &&
                        l.IsOnLine == true )
                    .OrderByDescending( l => l.LastActivityDateTime )
                    .Select( l => new
                    {
                        login = l,
                        pageViews = pageViewQry
                            .Where( v => v.PersonAlias.PersonId == l.PersonId )
                            .OrderByDescending( v => v.DateTimeViewed )
                            .Take( pageViewCount.Value )
                    } )
                    .Where( a =>
                        a.pageViews.Any() &&
                        a.pageViews.FirstOrDefault().SiteId == site.Id
                    );

                if ( CurrentUser != null )
                {
                    activeLogins = activeLogins.Where( m => m.login.UserName != CurrentUser.UserName );
                }

                foreach ( var activeLogin in activeLogins )
                {
                    var login = activeLogin.login;
                    var pageViews = activeLogin.pageViews.ToList();
                    Guid? latestSession = pageViews.FirstOrDefault().SessionId;

                    string pageViewsHtml = activeLogin.pageViews.ToList()
                                            .Where( v => v.SessionId == latestSession )
                                            .Select( v => HttpUtility.HtmlEncode( v.Page.PageTitle ) ).ToList().AsDelimited( "<br> " );

                    TimeSpan tsLastActivity = RockDateTime.Now.Subtract( (DateTime)login.LastActivityDateTime );
                    string className = tsLastActivity.Minutes <= 5 ? "recent" : "not-recent";

                    // create link to the person
                    string personLink = login.Person.FullName;

                    if ( GetAttributeValue( "PersonProfilePage" ) != null )
                    {
                        string personProfilePage = GetAttributeValue( "PersonProfilePage" );
                        var pageParams = new Dictionary<string, string>();
                        pageParams.Add( "PersonId", login.Person.Id.ToString() );
                        var pageReference = new Rock.Web.PageReference( personProfilePage, pageParams );
                        personLink = String.Format( @"<a href='{0}'>{1}</a>", pageReference.BuildUrl(), login.Person.FullName );
                    }

                    // determine whether to show last page views
                    if ( GetAttributeValue( "PageViewCount" ).AsInteger() > 0 )
                    {
                        sbUsers.Append( String.Format( @"<li class='active-user {0}' data-toggle='tooltip' data-placement='top' title='{2}'>
                                                                <i class='fa-li fa fa-circle'></i> {1}
                                                        </li>",
                                        className, personLink, pageViewsHtml ) );
                    }
                    else
                    {
                        sbUsers.Append( String.Format( @"<li class='active-user {0}'>
                                                                <i class='fa-li fa fa-circle'></i> {1}
                                                        </li>",
                                        className, personLink ) );
                    }
                }

                if ( sbUsers.Length > 0 )
                {
                    lUsers.Text = String.Format( @"<ul class='activeusers fa-ul'>{0}</ul>", sbUsers.ToString() );
                }
                else
                {
                    lMessages.Text = String.Format( "There are no logged in users on the {0} site.", site.Name );
                }

            }
            else
            {
                lMessages.Text = "<div class='alert alert-warning'>No site is currently configured.</div>";
            }
        }

        #endregion
    }
}