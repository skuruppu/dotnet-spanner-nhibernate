﻿// Copyright 2021 Google Inc. All Rights Reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using System;
using System.Collections.Generic;

namespace Google.Cloud.Spanner.NHibernate.IntegrationTests.SampleEntities
{
    public class Concert
    {
        public Concert()
        {
        }

        public virtual string Id { get; set; }
        public virtual Venue Venue { get; set; }
        public virtual DateTime StartTime { get; set; }
        public virtual Singer Singer { get; set; }
        public virtual string Title { get; set; }
        public virtual IList<Performance> Performances { get; set; }
    }

    public class ConcertMapping : ClassMapping<Concert>
    {
        public ConcertMapping()
        {
            Persister<SpannerSingleTableEntityPersister>();
            DynamicUpdate(true);
            Table("Concerts");
            Id(x => x.Id, m =>
            {
                m.Generator(new UUIDHexGeneratorDef());
                m.Length(36);
            });
            ManyToOne(x => x.Venue, m =>
            {
                m.NotNullable(true);
                m.Column(c => c.Length(10));
                m.ForeignKey("FK_Concerts_Venues");
            });
            Property(x => x.StartTime, m => m.NotNullable(true));
            ManyToOne(x => x.Singer, m =>
            {
                m.NotNullable(true);
                m.Column(c => c.Length(36));
                m.ForeignKey("FK_Concerts_Singers");
            });
            Property(x => x.Title, m => m.Length(200));
            Bag(x => x.Performances, c => { }, r => r.OneToMany());
        }
    }
}
