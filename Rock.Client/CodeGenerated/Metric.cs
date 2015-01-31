//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the Rock.CodeGeneration project
//     Changes to this file will be lost when the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
// <copyright>
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


namespace Rock.Client
{
    /// <summary>
    /// Simple Client Model for Metric
    /// </summary>
    public partial class Metric
    {
        /// <summary />
        public int Id { get; set; }

        /// <summary />
        public int? AdminPersonAliasId { get; set; }

        /// <summary />
        public int? DataViewId { get; set; }

        /// <summary />
        public string Description { get; set; }

        /// <summary />
        public EntityType EntityType { get; set; }

        /// <summary />
        public int? EntityTypeId { get; set; }

        /// <summary />
        public string IconCssClass { get; set; }

        /// <summary />
        public bool IsCumulative { get; set; }

        /// <summary />
        public bool IsSystem { get; set; }

        /// <summary />
        public DateTime? LastRunDateTime { get; set; }

        /// <summary />
        public ICollection<MetricCategory> MetricCategories { get; set; }

        /// <summary />
        public int? MetricChampionPersonAliasId { get; set; }

        /// <summary />
        public int? ScheduleId { get; set; }

        /// <summary />
        public string SourceSql { get; set; }

        /// <summary />
        public DefinedValue SourceValueType { get; set; }

        /// <summary />
        public int? SourceValueTypeId { get; set; }

        /// <summary />
        public string Subtitle { get; set; }

        /// <summary />
        public string Title { get; set; }

        /// <summary />
        public string XAxisLabel { get; set; }

        /// <summary />
        public string YAxisLabel { get; set; }

        /// <summary />
        public DateTime? CreatedDateTime { get; set; }

        /// <summary />
        public DateTime? ModifiedDateTime { get; set; }

        /// <summary />
        public int? CreatedByPersonAliasId { get; set; }

        /// <summary />
        public int? ModifiedByPersonAliasId { get; set; }

        /// <summary />
        public Guid Guid { get; set; }

        /// <summary />
        public string ForeignId { get; set; }

    }
}
