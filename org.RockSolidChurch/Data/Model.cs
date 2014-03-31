﻿//
// Copyright (C) Rock Solid Church - All Rights Reserved
//
using System.Runtime.Serialization;

namespace org.RockSolidChurch.Data
{
    [DataContract]
    public class Model<T> : Rock.Data.Model<T> where T : Rock.Data.Model<T>, Rock.Security.ISecured, new()
    {
    }
}
