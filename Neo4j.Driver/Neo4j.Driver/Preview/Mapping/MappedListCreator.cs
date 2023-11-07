﻿// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
// 
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Neo4j.Driver.Preview.Mapping;

internal interface IMappedListCreator
{
    IList CreateMappedList(IEnumerable list, Type desiredListType, IRecord record);
}

internal class MappedListCreator : IMappedListCreator
{
    private readonly IRecordObjectMapping _recordObjectMapping = RecordObjectMapping.Instance;

    public IList CreateMappedList(IEnumerable list, Type desiredListType, IRecord record)
    {
        var newList = (IList)Activator.CreateInstance(desiredListType);
        var desiredItemType = desiredListType.GetGenericArguments()[0];

        foreach (var item in list)
        {
            // entities and dictionaries can use the same logic, we can make them both into dictionaries
            var dict = item switch
            {
                IEntity entity => entity.Properties,
                IReadOnlyDictionary<string, object> dictionary => dictionary,
                _ => null
            };

            if (dict is not null)
            {
                // if the item is an entity or dictionary, we need to make it into a record and then map that
                var subRecord = new DictAsRecord(dict, record);
                var newItem = _recordObjectMapping.Map(subRecord, desiredItemType);
                newList!.Add(newItem);
            }
            else
            {
                // otherwise, just convert the item to the type of the list
                newList!.Add(item.AsType(desiredItemType));
            }
        }

        return newList;
    }
}
