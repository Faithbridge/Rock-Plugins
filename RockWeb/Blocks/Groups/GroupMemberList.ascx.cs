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
using System.Data.Entity;
using System.Linq;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.Groups
{
    [DisplayName( "Group Member List" )]
    [Category( "Groups" )]
    [Description( "Lists all the members of the given group." )]

    [GroupField( "Group", "Either pick a specific group or choose <none> to have group be determined by the groupId page parameter" )]
    [LinkedPage( "Detail Page" )]
    [LinkedPage( "Person Profile Page", "Page used for viewing a person's profile. If set a view profile button will show for each group member.", false, "", "", 2, "PersonProfilePage" )]
    public partial class GroupMemberList : RockBlock, ISecondaryBlock
    {
        #region Private Variables

        private DefinedValueCache _inactiveStatus = null;
        private Group _group = null;
        private bool _canView = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the available attributes.
        /// </summary>
        /// <value>
        /// The available attributes.
        /// </value>
        public List<AttributeCache> AvailableAttributes { get; set; }

        #endregion

        #region Control Methods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            AvailableAttributes = ViewState["AvailableAttributes"] as List<AttributeCache>;

            AddDynamicControls();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // if this block has a specific GroupId set, use that, otherwise, determine it from the PageParameters
            Guid groupGuid = GetAttributeValue( "Group" ).AsGuid();
            int groupId = 0;

            if ( groupGuid == Guid.Empty )
            {
                groupId = PageParameter( "GroupId" ).AsInteger();
            }

            if ( !( groupId == 0 && groupGuid == Guid.Empty ) )
            {
                string key = string.Format( "Group:{0}", groupId );
                _group = RockPage.GetSharedItem( key ) as Group;
                if ( _group == null )
                {
                    _group = new GroupService( new RockContext() ).Queryable( "GroupType.Roles" )
                        .Where( g => g.Id == groupId || g.Guid == groupGuid )
                        .FirstOrDefault();
                    RockPage.SaveSharedItem( key, _group );
                }

                if ( _group != null && _group.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
                {
                    _canView = true;

                    rFilter.ApplyFilterClick += rFilter_ApplyFilterClick;
                    gGroupMembers.DataKeyNames = new string[] { "Id" };
                    gGroupMembers.CommunicateMergeFields = new List<string> { "GroupRole" };
                    gGroupMembers.PersonIdField = "PersonId";
                    gGroupMembers.RowDataBound += gGroupMembers_RowDataBound;
                    gGroupMembers.Actions.AddClick += gGroupMembers_AddClick;
                    gGroupMembers.GridRebind += gGroupMembers_GridRebind;
                    gGroupMembers.RowItemText = _group.GroupType.GroupTerm + " " + _group.GroupType.GroupMemberTerm;
                    gGroupMembers.ExportFilename = _group.Name;
                    gGroupMembers.ExportSource = ExcelExportSource.DataSource;

                    // make sure they have Auth to edit the block OR edit to the Group
                    bool canEditBlock = IsUserAuthorized( Authorization.EDIT ) || _group.IsAuthorized( Authorization.EDIT, this.CurrentPerson );
                    gGroupMembers.Actions.ShowAdd = canEditBlock;
                    gGroupMembers.IsDeleteEnabled = canEditBlock;
                }

                // if group is being sync'ed remove ability to add/delete members 
                if ( _group != null && _group.SyncDataViewId.HasValue )
                {
                    gGroupMembers.IsDeleteEnabled = false;
                    gGroupMembers.Actions.ShowAdd = false;
                    hlSyncStatus.Visible = true;
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                pnlContent.Visible = _canView;
                if ( _canView )
                {
                    SetFilter();
                    BindGroupMembersGrid();
                }
            }
        }

        /// <summary>
        /// Saves any user control view-state changes that have occurred since the last page postback.
        /// </summary>
        /// <returns>
        /// Returns the user control's current view state. If there is no view state associated with the control, it returns null.
        /// </returns>
        protected override object SaveViewState()
        {
            ViewState["AvailableAttributes"] = AvailableAttributes;

            return base.SaveViewState();
        }

        #endregion

        #region GroupMembers Grid

        /// <summary>
        /// Handles the RowDataBound event of the gGroupMembers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Web.UI.WebControls.GridViewRowEventArgs"/> instance containing the event data.</param>
        protected void gGroupMembers_RowDataBound( object sender, System.Web.UI.WebControls.GridViewRowEventArgs e )
        {
            if ( e.Row.RowType == DataControlRowType.DataRow )
            {
                dynamic groupMember = e.Row.DataItem;

                if ( groupMember != null )
                {
                    if ( groupMember != null && groupMember.IsDeceased )
                    {
                        e.Row.AddCssClass( "is-deceased" );
                    }

                    if ( _inactiveStatus != null && groupMember.RecordStatusValueId == _inactiveStatus.Id )
                    {
                        e.Row.AddCssClass( "is-inactive-person" );
                    }

                    if ( _inactiveStatus != null && groupMember.GroupMemberStatus == GroupMemberStatus.Inactive )
                    {
                        e.Row.AddCssClass( "is-inactive" );
                    }
                }
            }
        }

        /// <summary>
        /// Handles the ApplyFilterClick event of the rFilter control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void rFilter_ApplyFilterClick( object sender, EventArgs e )
        {
            rFilter.SaveUserPreference( MakeKeyUniqueToGroup( "First Name" ), "First Name", tbFirstName.Text );
            rFilter.SaveUserPreference( MakeKeyUniqueToGroup( "Last Name" ), "Last Name", tbLastName.Text );
            rFilter.SaveUserPreference( MakeKeyUniqueToGroup( "Role" ), "Role", cblRole.SelectedValues.AsDelimited( ";" ) );
            rFilter.SaveUserPreference( MakeKeyUniqueToGroup( "Status" ), "Status", cblStatus.SelectedValues.AsDelimited( ";" ) );

            if ( AvailableAttributes != null )
            {
                foreach ( var attribute in AvailableAttributes )
                {
                    var filterControl = phAttributeFilters.FindControl( "filter_" + attribute.Id.ToString() );
                    if ( filterControl != null )
                    {
                        try
                        {
                            var values = attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter );
                            rFilter.SaveUserPreference( MakeKeyUniqueToGroup( attribute.Key ), attribute.Name, attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter ).ToJson() );
                        }
                        catch
                        {
                            // intentionally ignore
                        }
                    }
                }
            }

            BindGroupMembersGrid();
        }

        /// <summary>
        /// Rs the filter_ display filter value.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        protected void rFilter_DisplayFilterValue( object sender, GridFilter.DisplayFilterValueArgs e )
        {
            if ( AvailableAttributes != null )
            {
                var attribute = AvailableAttributes.FirstOrDefault( a => MakeKeyUniqueToGroup( a.Key ) == e.Key );
                if ( attribute != null )
                {
                    try
                    {
                        var values = JsonConvert.DeserializeObject<List<string>>( e.Value );
                        e.Value = attribute.FieldType.Field.FormatFilterValues( attribute.QualifierValues, values );
                        return;
                    }
                    catch
                    {
                        // intentionally ignore
                    }
                }
            }

            if ( e.Key == MakeKeyUniqueToGroup( "First Name" ) )
            {
                return;
            }
            else if ( e.Key == MakeKeyUniqueToGroup( "Last Name" ) )
            {
                return;
            }
            else if ( e.Key == MakeKeyUniqueToGroup( "Role" ) )
            {
                e.Value = ResolveValues( e.Value, cblRole );
            }
            else if ( e.Key == MakeKeyUniqueToGroup( "Status" ) )
            {
                e.Value = ResolveValues( e.Value, cblStatus );
            }
            else
            {
                e.Value = string.Empty;
            }
        }

        /// <summary>
        /// Handles the Click event of the DeleteGroupMember control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="Rock.Web.UI.Controls.RowEventArgs" /> instance containing the event data.</param>
        protected void DeleteGroupMember_Click( object sender, Rock.Web.UI.Controls.RowEventArgs e )
        {
            RockContext rockContext = new RockContext();
            GroupMemberService groupMemberService = new GroupMemberService( rockContext );
            GroupMember groupMember = groupMemberService.Get( e.RowKeyId );
            if ( groupMember != null )
            {
                string errorMessage;
                if ( !groupMemberService.CanDelete( groupMember, out errorMessage ) )
                {
                    mdGridWarning.Show( errorMessage, ModalAlertType.Information );
                    return;
                }

                int groupId = groupMember.GroupId;

                groupMemberService.Delete( groupMember );
                rockContext.SaveChanges();

                Group group = new GroupService( rockContext ).Get( groupId );
                if ( group.IsSecurityRole || group.GroupType.Guid.Equals( Rock.SystemGuid.GroupType.GROUPTYPE_SECURITY_ROLE.AsGuid() ) )
                {
                    // person removed from SecurityRole, Flush
                    Rock.Security.Role.Flush( group.Id );
                    Rock.Security.Authorization.Flush();
                }
            }

            BindGroupMembersGrid();
        }

        /// <summary>
        /// Handles the AddClick event of the gGroupMembers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        protected void gGroupMembers_AddClick( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "DetailPage", "GroupMemberId", 0, "GroupId", _group.Id );
        }

        /// <summary>
        /// Handles the Edit event of the gGroupMembers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs" /> instance containing the event data.</param>
        protected void gGroupMembers_Edit( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( "DetailPage", "GroupMemberId", e.RowKeyId );
        }

        /// <summary>
        /// Handles the GridRebind event of the gGroupMembers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        protected void gGroupMembers_GridRebind( object sender, EventArgs e )
        {
            BindGroupMembersGrid( !gGroupMembers.AllowPaging );
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Binds the filter.
        /// </summary>
        private void SetFilter()
        {
            if ( _group != null )
            {
                cblRole.DataSource = _group.GroupType.Roles.OrderBy( a => a.Order ).ToList();
                cblRole.DataBind();
            }

            cblStatus.BindToEnum<GroupMemberStatus>();

            BindAttributes();
            AddDynamicControls();

            tbFirstName.Text = rFilter.GetUserPreference( MakeKeyUniqueToGroup( "First Name" ) );
            tbLastName.Text = rFilter.GetUserPreference( MakeKeyUniqueToGroup( "Last Name" ) );

            string roleValue = rFilter.GetUserPreference( MakeKeyUniqueToGroup( "Role" ) );
            if ( !string.IsNullOrWhiteSpace( roleValue ) )
            {
                cblRole.SetValues( roleValue.Split( ';' ).ToList() );
            }

            string statusValue = rFilter.GetUserPreference( MakeKeyUniqueToGroup( "Status" ) );
            if ( !string.IsNullOrWhiteSpace( statusValue ) )
            {
                cblStatus.SetValues( statusValue.Split( ';' ).ToList() );
            }
        }

        /// <summary>
        /// Binds the attributes.
        /// </summary>
        private void BindAttributes()
        {
            // Parse the attribute filters 
            AvailableAttributes = new List<AttributeCache>();
            if ( _group != null )
            {
                int entityTypeId = new GroupMember().TypeId;
                string groupQualifier = _group.Id.ToString();
                string groupTypeQualifier = _group.GroupTypeId.ToString();
                foreach ( var attributeModel in new AttributeService( new RockContext() ).Queryable()
                    .Where( a =>
                        a.EntityTypeId == entityTypeId &&
                        a.IsGridColumn &&
                        ( ( a.EntityTypeQualifierColumn.Equals( "GroupId", StringComparison.OrdinalIgnoreCase ) && a.EntityTypeQualifierValue.Equals( groupQualifier ) ) ||
                         ( a.EntityTypeQualifierColumn.Equals( "GroupTypeId", StringComparison.OrdinalIgnoreCase ) && a.EntityTypeQualifierValue.Equals( groupTypeQualifier ) ) ) )
                    .OrderByDescending( a => a.EntityTypeQualifierColumn )
                    .ThenBy( a => a.Order )
                    .ThenBy( a => a.Name ) )
                {
                    AvailableAttributes.Add( AttributeCache.Read( attributeModel ) );
                }
            }
        }

        /// <summary>
        /// Adds the attribute columns.
        /// </summary>
        private void AddDynamicControls()
        {
            // Clear the filter controls
            phAttributeFilters.Controls.Clear();

            // Remove attribute columns
            foreach ( var column in gGroupMembers.Columns.OfType<AttributeField>().ToList() )
            {
                gGroupMembers.Columns.Remove( column );
            }

            if ( AvailableAttributes != null )
            {
                foreach ( var attribute in AvailableAttributes )
                {
                    var control = attribute.FieldType.Field.FilterControl( attribute.QualifierValues, "filter_" + attribute.Id.ToString(), false, Rock.Reporting.FilterMode.SimpleFilter );
                    if ( control != null )
                    {
                        if ( control is IRockControl )
                        {
                            var rockControl = (IRockControl)control;
                            rockControl.Label = attribute.Name;
                            rockControl.Help = attribute.Description;
                            phAttributeFilters.Controls.Add( control );
                        }
                        else
                        {
                            var wrapper = new RockControlWrapper();
                            wrapper.ID = control.ID + "_wrapper";
                            wrapper.Label = attribute.Name;
                            wrapper.Controls.Add( control );
                            phAttributeFilters.Controls.Add( wrapper );
                        }

                        string savedValue = rFilter.GetUserPreference( MakeKeyUniqueToGroup( attribute.Key ) );
                        if ( !string.IsNullOrWhiteSpace( savedValue ) )
                        {
                            try
                            {
                                var values = JsonConvert.DeserializeObject<List<string>>( savedValue );
                                attribute.FieldType.Field.SetFilterValues( control, attribute.QualifierValues, values );
                            }
                            catch
                            {
                                // intentionally ignore
                            }
                        }
                    }

                    string dataFieldExpression = attribute.Key;
                    bool columnExists = gGroupMembers.Columns.OfType<AttributeField>().FirstOrDefault( a => a.DataField.Equals( dataFieldExpression ) ) != null;
                    if ( !columnExists )
                    {
                        AttributeField boundField = new AttributeField();
                        boundField.DataField = dataFieldExpression;
                        boundField.HeaderText = attribute.Name;
                        boundField.SortExpression = string.Empty;

                        var attributeCache = Rock.Web.Cache.AttributeCache.Read( attribute.Id );
                        if ( attributeCache != null )
                        {
                            boundField.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                        }

                        gGroupMembers.Columns.Add( boundField );
                    }
                }
            }

            // Add Place Elsewhere column if the group or group type has any Place Elsewhere member triggers
            if ( _group != null && _group.GroupType != null )
            {
                if ( _group.GetGroupMemberWorkflowTriggers().Where( a => a.TriggerType == GroupMemberWorkflowTriggerType.MemberPlacedElsewhere ).Any() )
                {
                    AddPlaceElsewhereColumn();
                }
            }

            // Add Link to Profile Page Column
            if ( !string.IsNullOrEmpty( GetAttributeValue( "PersonProfilePage" ) ) )
            {
                AddPersonProfileLinkColumn();
            }

            // Add delete column
            var deleteField = new DeleteField();
            gGroupMembers.Columns.Add( deleteField );
            deleteField.Click += DeleteGroupMember_Click;
        }

        /// <summary>
        /// Adds the column with a link to profile page.
        /// </summary>
        private void AddPersonProfileLinkColumn()
        {
            HyperLinkField hlPersonProfileLink = new HyperLinkField();
            hlPersonProfileLink.ItemStyle.HorizontalAlign = HorizontalAlign.Center;
            hlPersonProfileLink.HeaderStyle.CssClass = "grid-columncommand";
            hlPersonProfileLink.ItemStyle.CssClass = "grid-columncommand";
            hlPersonProfileLink.DataNavigateUrlFields = new string[1] { "PersonId" };
            hlPersonProfileLink.DataNavigateUrlFormatString = LinkedPageUrl( "PersonProfilePage", new Dictionary<string, string> { { "PersonId", "###" } } ).Replace( "###", "{0}" );
            hlPersonProfileLink.DataTextFormatString = "<div class='btn btn-default btn-sm'><i class='fa fa-user'></i></div>";
            hlPersonProfileLink.DataTextField = "PersonId";
            gGroupMembers.Columns.Add( hlPersonProfileLink );
        }

        /// <summary>
        /// Adds the Place Elsewhere column
        /// </summary>
        private void AddPlaceElsewhereColumn()
        {
            LinkButtonField btnPlaceElsewhere = new LinkButtonField();
            btnPlaceElsewhere.ItemStyle.HorizontalAlign = HorizontalAlign.Center;
            btnPlaceElsewhere.HeaderStyle.CssClass = "grid-columncommand";
            btnPlaceElsewhere.ItemStyle.CssClass = "grid-columncommand";
            btnPlaceElsewhere.Text = "<i class='fa fa-share'></i>";
            btnPlaceElsewhere.CssClass = "btn btn-default btn-sm";
            btnPlaceElsewhere.ToolTip = "Place Elsewhere";
            btnPlaceElsewhere.Click += btnPlaceElsewhere_Click;

            gGroupMembers.Columns.Add( btnPlaceElsewhere );
        }

        /// <summary>
        /// Handles the Click event of the btnPlaceElsewhere control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs" /> instance containing the event data.</param>
        protected void btnPlaceElsewhere_Click( object sender, RowEventArgs e )
        {
            var rockContext = new RockContext();

            var groupMemberPerson = new GroupMemberService( rockContext ).GetPerson( e.RowKeyId );
            if ( groupMemberPerson != null )
            {
                hfPlaceElsewhereGroupMemberId.Value = e.RowKeyId.ToString();
                lPlaceElsewhereGroupMemberName.Text = groupMemberPerson.ToString();
                BindPlaceElsewhereTriggerButtons( true );

                mdPlaceElsewhere.Visible = true;
                mdPlaceElsewhere.Show();
            }
        }

        /// <summary>
        /// Binds the place elsewhere trigger buttons.
        /// </summary>
        /// <param name="setDefault">if set to <c>true</c> [set default].</param>
        private void BindPlaceElsewhereTriggerButtons( bool setDefault )
        {
            var sortedTriggerList = _group.GetGroupMemberWorkflowTriggers().Where( a => a.TriggerType == GroupMemberWorkflowTriggerType.MemberPlacedElsewhere ).ToList();

            if ( setDefault )
            {
                var defaultTrigger = sortedTriggerList.FirstOrDefault();
                hfPlaceElsewhereTriggerId.Value = defaultTrigger != null ? defaultTrigger.Id.ToString() : null;
            }

            // if only one trigger, just show the name of it (don't show the button list)
            if ( sortedTriggerList.Count == 1 )
            {
                rcwSelectMemberTrigger.Visible = false;

                lWorkflowTriggerName.Visible = true;
                lWorkflowTriggerName.Text = sortedTriggerList[0].Name;
            }
            else
            {
                lWorkflowTriggerName.Visible = false;

                rcwSelectMemberTrigger.Visible = true;
                rptSelectMemberTrigger.DataSource = sortedTriggerList.OrderBy( a => a.WorkflowName );
                rptSelectMemberTrigger.DataBind();
            }

            var selectedTrigger = sortedTriggerList.Where( a => a.Id == hfPlaceElsewhereTriggerId.Value.AsInteger() ).FirstOrDefault();
            if ( selectedTrigger != null )
            {
                var qualifierParts = ( selectedTrigger.TypeQualifier ?? string.Empty ).Split( new char[] { '|' } );
                bool showNote = qualifierParts.Length > 5 ? qualifierParts[5].AsBoolean() : false;
                bool requireNote = qualifierParts.Length > 6 ? qualifierParts[6].AsBoolean() : false;
                tbPlaceElsewhereNote.Visible = showNote || requireNote;
                tbPlaceElsewhereNote.Required = requireNote;
                lWorkflowName.Text = selectedTrigger.WorkflowType.ToString();
            }
        }

        /// <summary>
        /// Handles the Click event of the btnMemberTrigger control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnMemberTrigger_Click( object sender, EventArgs e )
        {
            var btnMemberTrigger = sender as LinkButton;
            if ( btnMemberTrigger != null )
            {
                hfPlaceElsewhereTriggerId.Value = btnMemberTrigger.CommandArgument;
                BindPlaceElsewhereTriggerButtons( false );
            }
        }

        /// <summary>
        /// Handles the ItemDataBound event of the rptSelectMemberTrigger control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RepeaterItemEventArgs"/> instance containing the event data.</param>
        protected void rptSelectMemberTrigger_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            var btnMemberTrigger = e.Item.FindControl( "btnMemberTrigger" ) as LinkButton;
            var trigger = e.Item.DataItem as GroupMemberWorkflowTrigger;
            if ( trigger != null && trigger.Id == hfPlaceElsewhereTriggerId.Value.AsInteger() )
            {
                btnMemberTrigger.AddCssClass( "active" );
            }
            else
            {
                btnMemberTrigger.RemoveCssClass( "active" );
            }
        }

        /// <summary>
        /// Handles the SaveClick event of the mdPlaceElsewhere control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void mdPlaceElsewhere_SaveClick( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var groupService = new GroupService( rockContext );
                var groupMemberService = new GroupMemberService( rockContext );
                var groupMember = groupMemberService.Get( hfPlaceElsewhereGroupMemberId.Value.AsInteger() );
                if ( groupMember != null )
                {
                    string errorMessage;
                    if ( !groupMemberService.CanDelete( groupMember, out errorMessage ) )
                    {
                        nbPlaceElsewhereWarning.Text = errorMessage;
                        return;
                    }

                    
                    var trigger = _group.GetGroupMemberWorkflowTriggers().FirstOrDefault( a => a.Id == hfPlaceElsewhereTriggerId.Value.AsInteger() );
                    if ( trigger != null )
                    {
                        // create a transaction for the selected trigger
                        var transaction = new Rock.Transactions.GroupMemberPlacedElsewhereTransaction( groupMember, tbPlaceElsewhereNote.Text, trigger );

                        // delete the group member from the current group
                        groupMemberService.Delete( groupMember );

                        rockContext.SaveChanges();

                        // queue up the transaction
                        Rock.Transactions.RockQueue.TransactionQueue.Enqueue( transaction );
                    }
                }

                mdPlaceElsewhere.Hide();
                mdPlaceElsewhere.Visible = false;
                BindGroupMembersGrid();
            }
        }

        /// <summary>
        /// Binds the group members grid.
        /// </summary>
        protected void BindGroupMembersGrid( bool selectAll = false )
        {
            if ( _group != null )
            {
                pnlGroupMembers.Visible = true;

                lHeading.Text = string.Format( "{0} {1}", _group.GroupType.GroupTerm, _group.GroupType.GroupMemberTerm.Pluralize() );

                if ( _group.GroupType.Roles.Any() )
                {
                    nbRoleWarning.Visible = false;
                    rFilter.Visible = true;
                    gGroupMembers.Visible = true;

                    var rockContext = new RockContext();

                    GroupMemberService groupMemberService = new GroupMemberService( rockContext );
                    var qry = groupMemberService.Queryable( "Person,GroupRole", true ).AsNoTracking()
                        .Where( m => m.GroupId == _group.Id );

                    // Filter by First Name
                    string firstName = tbFirstName.Text;
                    if ( !string.IsNullOrWhiteSpace( firstName ) )
                    {
                        qry = qry.Where( m => m.Person.FirstName.StartsWith( firstName ) );
                    }

                    // Filter by Last Name
                    string lastName = tbLastName.Text;
                    if ( !string.IsNullOrWhiteSpace( lastName ) )
                    {
                        qry = qry.Where( m => m.Person.LastName.StartsWith( lastName ) );
                    }

                    // Filter by role
                    var validGroupTypeRoles = _group.GroupType.Roles.Select( r => r.Id ).ToList();
                    var roles = new List<int>();
                    foreach ( string role in cblRole.SelectedValues )
                    {
                        if ( !string.IsNullOrWhiteSpace( role ) )
                        {
                            int roleId = int.MinValue;
                            if ( int.TryParse( role, out roleId ) && validGroupTypeRoles.Contains( roleId ) )
                            {
                                roles.Add( roleId );
                            }
                        }
                    }

                    if ( roles.Any() )
                    {
                        qry = qry.Where( m => roles.Contains( m.GroupRoleId ) );
                    }

                    // Filter by Status
                    var statuses = new List<GroupMemberStatus>();
                    foreach ( string status in cblStatus.SelectedValues )
                    {
                        if ( !string.IsNullOrWhiteSpace( status ) )
                        {
                            statuses.Add( status.ConvertToEnum<GroupMemberStatus>() );
                        }
                    }

                    if ( statuses.Any() )
                    {
                        qry = qry.Where( m => statuses.Contains( m.GroupMemberStatus ) );
                    }

                    // Filter query by any configured attribute filters
                    if ( AvailableAttributes != null && AvailableAttributes.Any() )
                    {
                        var attributeValueService = new AttributeValueService( rockContext );
                        var parameterExpression = attributeValueService.ParameterExpression;

                        foreach ( var attribute in AvailableAttributes )
                        {
                            var filterControl = phAttributeFilters.FindControl( "filter_" + attribute.Id.ToString() );
                            if ( filterControl != null )
                            {
                                var filterValues = attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter );
                                var expression = attribute.FieldType.Field.AttributeFilterExpression( attribute.QualifierValues, filterValues, parameterExpression );
                                if ( expression != null )
                                {
                                    var attributeValues = attributeValueService
                                        .Queryable()
                                        .Where( v => v.Attribute.Id == attribute.Id );

                                    attributeValues = attributeValues.Where( parameterExpression, expression, null );

                                    qry = qry.Where( w => attributeValues.Select( v => v.EntityId ).Contains( w.Id ) );
                                }
                            }
                        }
                    }

                    _inactiveStatus = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE );

                    SortProperty sortProperty = gGroupMembers.SortProperty;

                    bool hasGroupRequirements = new GroupRequirementService( rockContext ).Queryable().Where( a => a.GroupId == _group.Id ).Any();

                    // If there are group requirements that that member doesn't meet, show an icon in the grid
                    bool includeWarnings = false;
                    var groupMemberIdsThatLackGroupRequirements = new GroupService( rockContext ).GroupMembersNotMeetingRequirements( _group.Id, includeWarnings ).Select( a => a.Key.Id );

                    List<GroupMember> groupMembersList = null;

                    if ( sortProperty != null )
                    {
                        groupMembersList = qry.Sort( sortProperty ).ToList();
                    }
                    else
                    {
                        groupMembersList = qry.OrderBy( a => a.GroupRole.Order ).ThenBy( a => a.Person.LastName ).ThenBy( a => a.Person.FirstName ).ToList();
                    }

                    // Since we're not binding to actual group member list, but are using AttributeField columns,
                    // we need to save the workflows into the grid's object list
                    gGroupMembers.ObjectList = new Dictionary<string, object>();
                    groupMembersList.ForEach( m => gGroupMembers.ObjectList.Add( m.Id.ToString(), m ) );
                    gGroupMembers.EntityTypeId = EntityTypeCache.Read( Rock.SystemGuid.EntityType.GROUP_MEMBER.AsGuid() ).Id;

                    var homePhoneType = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME );
                    var cellPhoneType = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE );

                    // If exporting to Excel, the selectAll option will be true, and home location should be calculated
                    var homeLocations = new Dictionary<int, Location>();
                    if ( selectAll )
                    {
                        foreach ( var m in groupMembersList )
                        {
                            homeLocations.Add( m.Id, m.Person.GetHomeLocation( rockContext ) );
                        }
                    }

                    gGroupMembers.DataSource = groupMembersList
                        .ToList().Select( m => new
                    {
                        m.Id,
                        m.Guid,
                        m.PersonId,
                        Name = m.Person.NickName + " " + m.Person.LastName
                            + ( hasGroupRequirements && groupMemberIdsThatLackGroupRequirements.Contains( m.Id )
                                ? " <i class='fa fa-exclamation-triangle text-warning'></i>"
                                : string.Empty )
                            + ( !string.IsNullOrEmpty( m.Note )
                            ? " <i class='fa fa-file-text-o text-info'></i>"
                            : string.Empty ),
                        Email = m.Person.Email,
                        HomePhone = homePhoneType != null ?
                            m.Person.PhoneNumbers
                                .Where( p => p.NumberTypeValueId.HasValue && p.NumberTypeValueId.Value == homePhoneType.Id )
                                .Select( p => p.NumberFormatted )
                                .FirstOrDefault() : string.Empty,
                        CellPhone = cellPhoneType != null ?
                            m.Person.PhoneNumbers
                                .Where( p => p.NumberTypeValueId.HasValue && p.NumberTypeValueId.Value == cellPhoneType.Id )
                                .Select( p => p.NumberFormatted )
                                .FirstOrDefault() : string.Empty,
                        HomeAddress = homeLocations.ContainsKey( m.Id ) && homeLocations[m.Id] != null ?
                            homeLocations[m.Id].FormattedAddress : string.Empty,
                        Latitude = homeLocations.ContainsKey( m.Id ) && homeLocations[m.Id] != null ?
                            homeLocations[m.Id].Latitude : (double?)null,
                        Longitude = homeLocations.ContainsKey( m.Id ) && homeLocations[m.Id] != null ?
                            homeLocations[m.Id].Longitude : (double?)null,
                        GroupRole = m.GroupRole.Name,
                        m.GroupMemberStatus,
                        RecordStatusValueId = m.Person.RecordStatusValueId,
                        IsDeceased = m.Person.IsDeceased
                    } ).ToList();
                    gGroupMembers.DataBind();
                }
                else
                {
                    nbRoleWarning.Text = string.Format(
                        "{0} cannot be added to this {1} because the '{2}' group type does not have any roles defined.",
                        _group.GroupType.GroupMemberTerm.Pluralize(),
                        _group.GroupType.GroupTerm,
                        _group.GroupType.Name );

                    nbRoleWarning.Visible = true;
                    rFilter.Visible = false;
                    gGroupMembers.Visible = false;
                }
            }
            else
            {
                pnlGroupMembers.Visible = false;
            }
        }

        /// <summary>
        /// Resolves the values.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="listControl">The list control.</param>
        /// <returns></returns>
        private string ResolveValues( string values, System.Web.UI.WebControls.CheckBoxList checkBoxList )
        {
            var resolvedValues = new List<string>();

            foreach ( string value in values.Split( ';' ) )
            {
                var item = checkBoxList.Items.FindByValue( value );
                if ( item != null )
                {
                    resolvedValues.Add( item.Text );
                }
            }

            return resolvedValues.AsDelimited( ", " );
        }

        /// <summary>
        /// Makes the key unique to group.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        private string MakeKeyUniqueToGroup( string key )
        {
            if ( _group != null )
            {
                return string.Format( "{0}-{1}", _group.Id, key );
            }

            return key;
        }

        #endregion

        #region ISecondaryBlock

        /// <summary>
        /// Sets the visible.
        /// </summary>
        /// <param name="visible">if set to <c>true</c> [visible].</param>
        public void SetVisible( bool visible )
        {
            pnlContent.Visible = visible;
        }

        #endregion


    }
}