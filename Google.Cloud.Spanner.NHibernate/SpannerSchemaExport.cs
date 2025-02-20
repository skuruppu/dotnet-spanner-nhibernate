// Copyright 2021 Google LLC
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

using Google.Api.Gax;
using Google.Cloud.Spanner.Connection;
using Google.Cloud.Spanner.NHibernate.Internal;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Id;
using NHibernate.Mapping;
using NHibernate.Tool.hbm2ddl;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Environment = NHibernate.Cfg.Environment;

namespace Google.Cloud.Spanner.NHibernate
{
    /// <summary>
    /// Schema export implementation specifically for Cloud Spanner. Use this instead of the normal
    /// <see cref="SchemaExport"/> when working with Cloud Spanner.
    ///
    /// This schema exporter will ensure that:
    /// 1. The generated DDL is compatible with Cloud Spanner.
    /// 2. DDL scripts are executed as one batch instead of as individual statements. This significantly improves the
    ///    execution speed of large DDL batches.
    /// </summary>
    public class SpannerSchemaExport : SchemaExport
    {
        private static readonly SimpleValue DisablePrimaryKeyGenerator = new SimpleValue { IdentifierGeneratorStrategy = typeof(IdentityGenerator).AssemblyQualifiedName };
        private static readonly Dialect ExportDialect = new SpannerSchemaExportDialect();
        
        private readonly Dictionary<Table, IKeyValue> _primaryKeysGenerators = new Dictionary<Table, IKeyValue>();
        private readonly Dictionary<Table, string> _tableComments = new Dictionary<Table, string>();
        private readonly Dictionary<Column, string> _columnDefaultValues = new Dictionary<Column, string>();

        private readonly Configuration _configuration;


        public SpannerSchemaExport(Configuration cfg) : this(cfg, cfg.Properties)
        {
        }

        public SpannerSchemaExport(Configuration cfg, IDictionary<string, string> configProperties)
            : base(cfg, ReplaceDialectAndConnectionProvider(configProperties))
        {
            _configuration = cfg;
        }

        /// <inheritdoc cref="SchemaExport.Create(bool,bool)"/>
        public new void Create(bool useStdOut, bool execute) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Create(useStdOut, execute));

        /// <inheritdoc cref="SchemaExport.CreateAsync(bool,bool,CancellationToken)"/>
        public new async Task CreateAsync(bool useStdOut, bool execute, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.CreateAsync(useStdOut, execute, cancellationToken));

        /// <inheritdoc cref="SchemaExport.Create(bool,bool,DbConnection)"/>
        public new void Create(bool useStdOut, bool execute, DbConnection connection) =>
            ExecuteWithWrappedConnection(conn => ExecuteWithPrimaryKeysAsComment(() => base.Create(useStdOut, execute, conn)), connection);

        /// <inheritdoc cref="SchemaExport.CreateAsync(bool,bool,DbConnection,CancellationToken)"/>
        public new async Task CreateAsync(bool useStdOut, bool execute, DbConnection connection, CancellationToken cancellationToken = default) =>
            await ExecuteWithWrappedConnectionAsync(conn => ExecuteWithPrimaryKeysAsCommentAsync(() => base.CreateAsync(useStdOut, execute, conn, cancellationToken)), connection);

        /// <inheritdoc cref="SchemaExport.Create(Action&lt;string&gt;,bool)"/>
        public new void Create(Action<string> scriptAction, bool execute) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Create(scriptAction, execute));

        /// <inheritdoc cref="SchemaExport.CreateAsync(Action&lt;string&gt;,bool,CancellationToken)"/>
        public new async Task CreateAsync(Action<string> scriptAction, bool execute, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.CreateAsync(scriptAction, execute, cancellationToken));

        /// <inheritdoc cref="SchemaExport.Create(Action&lt;string&gt;,bool,DbConnection)"/>
        public new void Create(Action<string> scriptAction, bool execute, DbConnection connection) =>
            ExecuteWithWrappedConnection(conn => ExecuteWithPrimaryKeysAsComment(() => base.Create(scriptAction, execute, conn)), connection);

        /// <inheritdoc cref="SchemaExport.CreateAsync(Action&lt;string&gt;,bool,DbConnection,CancellationToken)"/>
        public new async Task CreateAsync(Action<string> scriptAction, bool execute, DbConnection connection, CancellationToken cancellationToken = default) =>
            await ExecuteWithWrappedConnectionAsync(conn => ExecuteWithPrimaryKeysAsCommentAsync(() => base.CreateAsync(scriptAction, execute, conn, cancellationToken)), connection);
        
        /// <inheritdoc cref="SchemaExport.Create(TextWriter,bool)"/>
        public new void Create(TextWriter exportOutput, bool execute) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Create(exportOutput, execute));
        
        /// <inheritdoc cref="SchemaExport.CreateAsync(TextWriter,bool,CancellationToken)"/>
        public new async Task CreateAsync(TextWriter exportOutput, bool execute, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.CreateAsync(exportOutput, execute, cancellationToken));

        /// <inheritdoc cref="SchemaExport.Create(TextWriter,bool,DbConnection)"/>
        public new void Create(TextWriter exportOutput, bool execute, DbConnection connection) =>
            ExecuteWithWrappedConnection(conn => ExecuteWithPrimaryKeysAsComment(() => base.Create(exportOutput, execute, conn)), connection);

        /// <inheritdoc cref="SchemaExport.CreateAsync(TextWriter,bool,DbConnection,CancellationToken)"/>
        public new async Task CreateAsync(TextWriter exportOutput, bool execute, DbConnection connection, CancellationToken cancellationToken = default) =>
            await ExecuteWithWrappedConnectionAsync(conn => ExecuteWithPrimaryKeysAsCommentAsync(() => base.CreateAsync(exportOutput, execute, conn, cancellationToken)), connection);

        /// <inheritdoc cref="SchemaExport.Drop(bool,bool)"/>
        public new void Drop(bool useStdOut, bool execute) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Drop(useStdOut, execute));

        /// <inheritdoc cref="SchemaExport.DropAsync(bool,bool,CancellationToken)"/>
        public new async Task DropAsync(bool useStdOut, bool execute, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.DropAsync(useStdOut, execute, cancellationToken));

        /// <inheritdoc cref="SchemaExport.Drop(bool,bool,DbConnection)"/>
        public new void Drop(bool useStdOut, bool execute, DbConnection connection) =>
            ExecuteWithWrappedConnection(conn => ExecuteWithPrimaryKeysAsComment(() => base.Drop(useStdOut, execute, conn)), connection);

        /// <inheritdoc cref="SchemaExport.DropAsync(bool,bool,DbConnection,CancellationToken)"/>
        public new async Task DropAsync(bool useStdOut, bool execute, DbConnection connection, CancellationToken cancellationToken = default) =>
            await ExecuteWithWrappedConnectionAsync(conn => ExecuteWithPrimaryKeysAsCommentAsync(() => base.DropAsync(useStdOut, execute, conn, cancellationToken)), connection);

        /// <inheritdoc cref="SchemaExport.Drop(TextWriter,bool)"/>
        public new void Drop(TextWriter exportOutput, bool execute) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Drop(exportOutput, execute));

        /// <inheritdoc cref="SchemaExport.DropAsync(TextWriter,bool,CancellationToken)"/>
        public new async Task DropAsync(TextWriter exportOutput, bool execute, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.DropAsync(exportOutput, execute, cancellationToken));

        /// <inheritdoc cref="SchemaExport.Drop(TextWriter,bool,DbConnection)"/>
        public new void Drop(TextWriter exportOutput, bool execute, DbConnection connection) =>
            ExecuteWithWrappedConnection(conn => ExecuteWithPrimaryKeysAsComment(() => base.Drop(exportOutput, execute, conn)), connection);

        /// <inheritdoc cref="SchemaExport.DropAsync(TextWriter,bool,DbConnection,CancellationToken)"/>
        public new async Task DropAsync(TextWriter exportOutput, bool execute, DbConnection connection, CancellationToken cancellationToken = default) =>
            await ExecuteWithWrappedConnectionAsync(conn => ExecuteWithPrimaryKeysAsCommentAsync(() => base.DropAsync(exportOutput, execute, conn, cancellationToken)), connection);

        /// <inheritdoc cref="SchemaExport.Execute(bool,bool,bool,DbConnection,TextWriter)"/>
        public new void Execute(bool useStdOut, bool execute, bool justDrop, DbConnection connection,
            TextWriter exportOutput) => ExecuteWithWrappedConnection(
            conn => ExecuteWithPrimaryKeysAsComment(() => base.Execute(useStdOut, execute, justDrop, conn, exportOutput)), connection);

        /// <inheritdoc cref="SchemaExport.ExecuteAsync(bool,bool,bool,DbConnection,TextWriter,CancellationToken)"/>
        public new async Task ExecuteAsync(bool useStdOut, bool execute, bool justDrop, DbConnection connection,
            TextWriter exportOutput, CancellationToken cancellationToken = default) =>
            await ExecuteWithWrappedConnectionAsync(conn => ExecuteWithPrimaryKeysAsCommentAsync(() => base.ExecuteAsync(useStdOut, execute, justDrop, conn, exportOutput, cancellationToken)), connection);
        
        /// <inheritdoc cref="SchemaExport.Execute(Action&lt;string&gt;,bool,bool,DbConnection,TextWriter)"/>
        public new void Execute(Action<string> scriptAction, bool execute, bool justDrop, DbConnection connection,
            TextWriter exportOutput) =>
            ExecuteWithWrappedConnection(conn => ExecuteWithPrimaryKeysAsComment(() => base.Execute(scriptAction, execute, justDrop, conn, exportOutput)), connection);

        /// <inheritdoc cref="SchemaExport.ExecuteAsync(Action&lt;string&gt;,bool,bool,DbConnection,TextWriter,CancellationToken)"/>
        public new async Task ExecuteAsync(Action<string> scriptAction, bool execute, bool justDrop,
            DbConnection connection,
            TextWriter exportOutput, CancellationToken cancellationToken = default) =>
            await ExecuteWithWrappedConnectionAsync(conn => ExecuteWithPrimaryKeysAsCommentAsync(() => base.ExecuteAsync(scriptAction, execute, justDrop, conn, exportOutput, cancellationToken)), connection);

        /// <inheritdoc cref="SchemaExport.Execute(bool,bool,bool)"/>
        public new void Execute(bool useStdOut, bool execute, bool justDrop) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Execute(useStdOut, execute, justDrop));

        /// <inheritdoc cref="SchemaExport.ExecuteAsync(bool,bool,bool,CancellationToken)"/>
        public new async Task ExecuteAsync(bool useStdOut, bool execute, bool justDrop, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.ExecuteAsync(useStdOut, execute, justDrop, cancellationToken));

        /// <inheritdoc cref="SchemaExport.Execute(Action&lt;string&gt;,bool,bool)"/>
        public new void Execute(Action<string> scriptAction, bool execute, bool justDrop) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Execute(scriptAction, execute, justDrop));

        /// <inheritdoc cref="SchemaExport.ExecuteAsync(Action&lt;string&gt;,bool,bool,CancellationToken)"/>
        public new async Task ExecuteAsync(Action<string> scriptAction, bool execute, bool justDrop, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.ExecuteAsync(scriptAction, execute, justDrop, cancellationToken));

        /// <inheritdoc cref="SchemaExport.Execute(Action&lt;string&gt;,bool,bool,TextWriter)"/>
        public new void Execute(Action<string> scriptAction, bool execute, bool justDrop, TextWriter exportOutput) =>
            ExecuteWithPrimaryKeysAsComment(() => base.Execute(scriptAction, execute, justDrop, exportOutput));

        /// <inheritdoc cref="SchemaExport.ExecuteAsync(Action&lt;string&gt;,bool,bool,TextWriter,CancellationToken)"/>
        public new async Task ExecuteAsync(Action<string> scriptAction, bool execute, bool justDrop, TextWriter exportOutput, CancellationToken cancellationToken = default) =>
            await ExecuteWithPrimaryKeysAsCommentAsync(() => base.ExecuteAsync(scriptAction, execute, justDrop, exportOutput, cancellationToken));

        private void ExecuteWithWrappedConnection(Action<DdlBatchConnection> action, DbConnection connection)
        {
            GaxPreconditions.CheckArgument(connection is SpannerRetriableConnection, nameof(connection),"This method can only be used with a SpannerRetriableConnection");
            var conn = new DdlBatchConnection((SpannerRetriableConnection) connection);
            action.Invoke(conn);
            if (conn.ExecutionException != null)
            {
                throw conn.ExecutionException;
            }
        }

        private async Task ExecuteWithWrappedConnectionAsync(Func<DdlBatchConnection, Task> action, DbConnection connection)
        {
            GaxPreconditions.CheckArgument(connection is SpannerRetriableConnection, nameof(connection),"This method can only be used with a SpannerRetriableConnection");
            var conn = new DdlBatchConnection((SpannerRetriableConnection) connection);
            await action.Invoke(conn);
            if (conn.ExecutionException != null)
            {
                throw conn.ExecutionException;
            }
        }

        private void ExecuteWithPrimaryKeysAsComment(Action action)
        {
            try
            {
                MovePrimaryKeysToComment(_configuration, _tableComments, _primaryKeysGenerators, _columnDefaultValues);
                action.Invoke();
            }
            finally
            {
                ResetPrimaryKeys(_configuration, _tableComments, _primaryKeysGenerators, _columnDefaultValues);
            }
        }
        
        private async Task ExecuteWithPrimaryKeysAsCommentAsync(Func<Task> action)
        {
            try
            {
                MovePrimaryKeysToComment(_configuration, _tableComments, _primaryKeysGenerators, _columnDefaultValues);
                await action.Invoke();
            }
            finally
            {
                ResetPrimaryKeys(_configuration, _tableComments, _primaryKeysGenerators, _columnDefaultValues);
            }
        }

        internal static IDictionary<string, string> ReplaceDialectAndConnectionProvider(IDictionary<string, string> properties)
        {
            lock (properties)
            {
                GaxPreconditions.CheckNotNull(properties, nameof(properties));
                var copy = new Dictionary<string, string>(properties)
                {
                    [Environment.Dialect] = typeof(SpannerSchemaExportDialect).AssemblyQualifiedName
                };
                if (copy.TryGetValue(Environment.ConnectionProvider, out var providerClass))
                {
                    copy[$"wrapped.{Environment.ConnectionProvider}"] = providerClass;
                }
                copy[Environment.ConnectionProvider] = typeof(DdlBatchConnectionProvider).AssemblyQualifiedName;
                return copy;
            }
        }

        internal static void MovePrimaryKeysToComment(Configuration configuration, Dictionary<Table, string> tableComments, Dictionary<Table, IKeyValue> primaryKeysGenerators, Dictionary<Column, string> columnDefaultValues)
        {
            foreach (var mapping in configuration.ClassMappings)
            {
                if (mapping.Table.IdentifierValue != DisablePrimaryKeyGenerator)
                {
                    tableComments[mapping.Table] = mapping.Table.Comment;
                    primaryKeysGenerators[mapping.Table] = mapping.Table.IdentifierValue;
                    mapping.Table.Comment = mapping.Table.PrimaryKey?.SqlConstraintString(ExportDialect, "");
                    mapping.Table.IdentifierValue = DisablePrimaryKeyGenerator;
                }
                foreach (var col in mapping.Table.ColumnIterator)
                {
                    if (!string.IsNullOrEmpty(col.DefaultValue))
                    {
                        columnDefaultValues[col] = col.DefaultValue;
                        col.DefaultValue = null;
                    }
                }
            }
            // Also add all indexes as auxiliary objects to the configuration so these can be dropped before any tables.
            // We cannot get the auxiliary objects that have already been added to the config, so we have to use a
            // custom property to remember that.
            // We also convert all unique keys into unique indexes.
            if (configuration.Properties.TryAdd("spanner.auxiliary.indexes", "true"))
            {
                foreach (var mapping in configuration.ClassMappings)
                {
                    foreach (var index in mapping.Table.IndexIterator)
                    {
                        configuration.AddAuxiliaryDatabaseObject(new IndexAsAuxiliaryObject(index.Name));
                    }
                    foreach (var uniqueKey in mapping.Table.UniqueKeyIterator)
                    {
                        configuration.AddAuxiliaryDatabaseObject(new UniqueKeyAsAuxiliaryObject(uniqueKey));
                    }
                }
            }
        }

        internal static void ResetPrimaryKeys(Configuration configuration, Dictionary<Table, string> tableComments, Dictionary<Table, IKeyValue> primaryKeysGenerators, Dictionary<Column, string> columnDefaultValues)
        {
            foreach (var mapping in configuration.ClassMappings)
            {
                if (mapping.Table.IdentifierValue == DisablePrimaryKeyGenerator)
                {
                    mapping.Table.Comment = tableComments[mapping.Table];
                    mapping.Table.IdentifierValue = primaryKeysGenerators[mapping.Table];
                }
                foreach (var col in mapping.Table.ColumnIterator)
                {
                    if (columnDefaultValues.ContainsKey(col))
                    {
                        col.DefaultValue = columnDefaultValues[col];
                    }
                }
            }
        }
    }
}