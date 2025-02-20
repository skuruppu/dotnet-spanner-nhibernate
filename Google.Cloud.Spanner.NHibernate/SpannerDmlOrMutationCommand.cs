// Copyright 2021 Google Inc. All Rights Reserved.
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

using Google.Cloud.Spanner.Connection;
using Google.Cloud.Spanner.Data;

namespace Google.Cloud.Spanner.NHibernate
{
    /// <summary>
    /// SpannerDmlOrMutationCommand is a DbCommand implementation that is generated by
    /// the Spanner NHibernate driver and that contains both a DML and Mutation command.
    /// The driver will by default execute the DML command, but if the command is part of
    /// a batch that is executed on a transaction that allows mutations to be used, the
    /// driver will execute the mutation command instead.
    /// </summary>
    public class SpannerDmlOrMutationCommand : SpannerRetriableCommand
    {
        public SpannerRetriableCommand MutationCommand { get; }

        public SpannerDmlOrMutationCommand(SpannerCommand dmlCommand, SpannerRetriableCommand mutationCommand) : base(dmlCommand)
        {
            MutationCommand = mutationCommand;
        }

        public override object Clone()
        {
            var dmlCommand = (SpannerRetriableCommand) base.Clone();
            var mutationCommand = (SpannerRetriableCommand) MutationCommand.Clone();
            var clone = new SpannerDmlOrMutationCommand(dmlCommand.SpannerCommand, mutationCommand);
            clone.Connection = dmlCommand.Connection;
            clone.Transaction = dmlCommand.Transaction;
            mutationCommand.Connection = dmlCommand.Connection;
            mutationCommand.Transaction = dmlCommand.Transaction;

            return clone;
        }
    }
}