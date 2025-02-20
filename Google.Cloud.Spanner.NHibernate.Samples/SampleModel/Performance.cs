﻿// Copyright 2021 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

namespace Google.Cloud.Spanner.NHibernate.Samples.SampleModel
{
    public class Performance : AbstractVersionedEntity
    {
        public virtual Concert Concert { get; set; }
        public virtual Track Track { get; set; }
        public virtual DateTime? StartTime { get; set; }
        public virtual double? Rating { get; set; }
    }

    public class PerformanceMapping : VersionedEntityMapping<Performance>
    {
        public PerformanceMapping()
        {
            Table("Performances");
            ManyToOne(x => x.Concert);
            ManyToOne(x => x.Track, m =>
            {
                m.Columns(
                    c => c.Name("AlbumId"),
                    c => c.Name("TrackNumber"));
            });
            Property(x => x.StartTime);
            Property(x => x.Rating);
        }
    }
}
