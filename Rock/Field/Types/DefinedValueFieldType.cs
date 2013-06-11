﻿//
// THIS WORK IS LICENSED UNDER A CREATIVE COMMONS ATTRIBUTION-NONCOMMERCIAL-
// SHAREALIKE 3.0 UNPORTED LICENSE:
// http://creativecommons.org/licenses/by-nc-sa/3.0/
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using Rock;
using Rock.Constants;

namespace Rock.Field.Types
{
    /// <summary>
    /// Field Type used to display a dropdown list of Defined Values for a specific Defined Type
    /// </summary>
    [Serializable]
    public class DefinedValueFieldType : FieldType
    {
        private const string DEFINED_TYPE_KEY = "definedtype";
        private const string ALLOW_MULTIPLE_KEY = "allowmultiple";

        /// <summary>
        /// Returns the field's current value(s)
        /// </summary>
        /// <param name="parentControl">The parent control.</param>
        /// <param name="value">Information about the value</param>
        /// <param name="configurationValues">The configuration values.</param>
        /// <param name="condensed">Flag indicating if the value should be condensed (i.e. for use in a grid column)</param>
        /// <returns></returns>
        public override string FormatValue( Control parentControl, string value, Dictionary<string, ConfigurationValue> configurationValues, bool condensed )
        {
            if ( !string.IsNullOrWhiteSpace( value ) )
            {
                try
                {
                    var definedValue = Rock.Web.Cache.DefinedValueCache.Read( Int32.Parse( value ) );
                    if ( definedValue != null )
                    {
                        return definedValue.Name;
                    }
                }
                catch { }

                return "Unknown Defined Value: " + value;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns a list of the configuration keys
        /// </summary>
        /// <returns></returns>
        public override List<string> ConfigurationKeys()
        {
            var configKeys = base.ConfigurationKeys();
            configKeys.Add( DEFINED_TYPE_KEY );
            configKeys.Add( ALLOW_MULTIPLE_KEY );
            return configKeys;
        }

        /// <summary>
        /// Creates the HTML controls required to configure this type of field
        /// </summary>
        /// <returns></returns>
        public override List<Control> ConfigurationControls()
        {
            var controls = base.ConfigurationControls();

            // build a drop down list of defined types (the one that gets selected is
            // used to build a list of defined values) 
            DropDownList ddl = new DropDownList();
            controls.Add( ddl );
            ddl.AutoPostBack = true;
            ddl.SelectedIndexChanged += OnQualifierUpdated;

            Rock.Model.DefinedTypeService definedTypeService = new Model.DefinedTypeService();
            foreach ( var definedType in definedTypeService.Queryable().OrderBy( d => d.Order ) )
            {
                ddl.Items.Add( new ListItem( definedType.Name, definedType.Id.ToString() ) );
            }

            // Add checkbox for deciding if the defined values list is renedered as a drop
            // down list or a checkbox list.
            CheckBox cb = new CheckBox();
            controls.Add( cb );
            cb.AutoPostBack = true;
            cb.CheckedChanged += OnQualifierUpdated;
            cb.Text = "Allow Multiple Values";
            return controls;
        }

        /// <summary>
        /// Gets the configuration value.
        /// </summary>
        /// <param name="controls">The controls.</param>
        /// <returns></returns>
        public override Dictionary<string, ConfigurationValue> ConfigurationValues( List<Control> controls )
        {
            Dictionary<string, ConfigurationValue> configurationValues = new Dictionary<string, ConfigurationValue>();
            configurationValues.Add( DEFINED_TYPE_KEY, new ConfigurationValue( "Defined Type", "The Defined Type to select values from", "" ) );
            configurationValues.Add( ALLOW_MULTIPLE_KEY, new ConfigurationValue( "", "When set, allows multiple defined type values to be selected.", "" ) );

            if ( controls != null && controls.Count == 2 )
            {
                if ( controls[0] != null && controls[0] is DropDownList )
                {
                    configurationValues[DEFINED_TYPE_KEY].Value = ( (DropDownList)controls[0] ).SelectedValue;
                }

                if ( controls[1] != null && controls[1] is CheckBox )
                {
                    configurationValues[ ALLOW_MULTIPLE_KEY ].Value = ( (CheckBox)controls[1] ).Checked.ToString();
                }
            }

            return configurationValues;
        }

        /// <summary>
        /// Sets the configuration value.
        /// </summary>
        /// <param name="controls"></param>
        /// <param name="configurationValues"></param>
        public override void SetConfigurationValues( List<Control> controls, Dictionary<string, ConfigurationValue> configurationValues )
        {
            if ( controls != null && controls.Count == 2 && configurationValues != null )
            {
                if ( controls[0] != null && controls[0] is DropDownList && configurationValues.ContainsKey( DEFINED_TYPE_KEY ) )
                {
                    ( (DropDownList)controls[0] ).SelectedValue = configurationValues[DEFINED_TYPE_KEY].Value;
                }

                if ( controls[1] != null && controls[1] is CheckBox && configurationValues.ContainsKey( ALLOW_MULTIPLE_KEY ) )
                {
                    ( (CheckBox)controls[1] ).Checked = configurationValues[ALLOW_MULTIPLE_KEY].Value.AsBoolean();
                }
            }
        }

        /// <summary>
        /// Creates the control(s) neccessary for prompting user for a new value
        /// </summary>
        /// <param name="configurationValues"></param>
        /// <returns>
        /// The control
        /// </returns>
        public override Control EditControl( Dictionary<string, ConfigurationValue> configurationValues, string id )
        {
            ListControl editControl;

            if ( configurationValues != null && configurationValues.ContainsKey( ALLOW_MULTIPLE_KEY ) && configurationValues[ ALLOW_MULTIPLE_KEY ].Value.AsBoolean() )
            {
                editControl = new CheckBoxList { ID = id }; 
                editControl.AddCssClass( "checkboxlist-group" );
            }
            else
            {
                editControl = new DropDownList { ID = id }; 
                // Provide the ability to set a "none" default value (unless the Required is true).
                editControl.Items.Add( new ListItem( None.Text, None.IdValue ) );
            }

            if ( configurationValues != null && configurationValues.ContainsKey( DEFINED_TYPE_KEY ) )
            {
                int definedTypeId = 0;
                if ( Int32.TryParse( configurationValues[DEFINED_TYPE_KEY].Value, out definedTypeId ) )
                {
                    Rock.Model.DefinedValueService definedValueService = new Model.DefinedValueService();
                    foreach ( var definedValue in definedValueService.GetByDefinedTypeId( definedTypeId ) )
                    {
                        editControl.Items.Add( new ListItem( definedValue.Name, definedValue.Id.ToString() ) );
                    }
                }
            }
            
            return editControl;
        }

        /// <summary>
        /// Reads new values entered by the user for the field
        /// </summary>
        /// <param name="control">Parent control that controls were added to in the CreateEditControl() method</param>
        /// <param name="configurationValues"></param>
        /// <returns></returns>
        public override string GetEditValue( Control control, Dictionary<string, ConfigurationValue> configurationValues )
        {
            if ( control != null && control is ListControl )
            {
                if ( control is DropDownList )
                {
                    return ( (ListControl)control ).SelectedValue;
                }
                else if ( control is CheckBoxList )
                {
                    string x =  ( (CheckBoxList)control ).Items.Cast<ListItem>()
                        .Where(i => i.Selected)
                        .Select( i => i.Value ).ToJson();

                    //return ( (CheckBoxList)control ).Items.Cast<ListItem>()
                    //    .Where(i => i.Selected)
                    //    .Select( i => i.Value ).ToJson();

                    IEnumerable<string> allChecked = ( (CheckBoxList)control ).Items.Cast<ListItem>()
                        .Where( i => i.Selected )
                        .Select( i => i.Value );

                    return string.Join( ",", allChecked );
                }
            }
            return null;
        }

        /// <summary>
        /// Sets the value.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="configurationValues"></param>
        /// <param name="value">The value.</param>
        public override void SetEditValue( Control control, Dictionary<string, ConfigurationValue> configurationValues, string value )
        {
            if ( value != null )
            {
                if ( control != null && control is ListControl )
                {
                    if ( control is DropDownList )
                    {
                        ( (ListControl)control ).SelectedValue = value;
                    }
                    else if ( control is CheckBoxList )
                    {
                        //List<string> values =  JsonConvert.DeserializeObject(value, typeof(List<string>)) as List<string>;
                        List<string> values = new List<string>();
                        values.AddRange( value.Split( ',' ) );

                        CheckBoxList cbl = (CheckBoxList)control;
                        foreach ( ListItem li in cbl.Items )
                        {
                            li.Selected = values.Contains( li.Value );
                        }
                    }
                }
            }
        }

    }
}