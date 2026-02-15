using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Snowflake.Data.Client;

namespace Snowflake.Data.Telemetry
{
    /// <summary>
    /// Central ActivitySource for Snowflake connector tracing.
    /// This class manages all tracing activities for the connector.
    /// </summary>
    internal static class SnowflakeActivitySourceHelper
    {
        // Common span names
        public const string OperationConnect = "snowflake.connection.open";
        public const string OperationDisconnect = "snowflake.connection.close";
        public const string OperationExecuteQuery = "snowflake.query.execute";
        public const string OperationExecuteNonQuery = "snowflake.query.execute_non_query";
        public const string OperationExecuteScalar = "snowflake.query.execute_scalar";
        public const string OperationFetchResult = "snowflake.result.fetch";
        public const string OperationAuthenticate = "snowflake.auth.authenticate";
        public const string OperationHttpRequest = "snowflake.http.request";
        public const string OperationFileTransferPut = "snowflake.file.put";
        public const string OperationFileTransferGet = "snowflake.file.get";
        public const string OperationChunkDownload = "snowflake.chunk.download";
        public const string OperationChunkParse = "snowflake.chunk.parse";

        // Attribute names (standard OTEL semantic conventions)
        public static class Tags
        {
            public const string ConnectionString = "db.connection_string";
            public const string DbSystem = "db.system";
            public const string DbName = "db.name";
            public const string DbUser = "db.user";
            public const string DbStatement = "db.statement";
            public const string DbWarehouse = "db.warehouse";
            public const string DbRole = "db.role";
            public const string DbSchema = "db.schema";
            public const string HttpMethod = "http.method";
            public const string HttpUrl = "http.url";
            public const string HttpStatusCode = "http.status_code";
            public const string ErrorType = "error.type";
            public const string ExceptionMessage = "exception.message";
            public const string ExceptionStacktrace = "exception.stacktrace";
            public const string StatusCode = "otel.status_code";

            // Snowflake-specific attributes
            public const string QueryId = "snowflake.query.id";
            public const string QueryStatus = "snowflake.query.status";
            public const string SessionId = "snowflake.session.id";
            public const string RequestId = "snowflake.request.id";
            public const string ChunkId = "snowflake.chunk.id";
            public const string ChunkSize = "snowflake.chunk.size";
            public const string RowCount = "snowflake.result.row_count";
        }

        internal const string ActivitySourceName = "Snowflake.Data";
        internal const int StatementMaxLen = 300;

        private static readonly ActivitySource activitySource = CreateActivitySource();

        internal static Activity? StartActivity(this SnowflakeDbConnection connection, string name)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            var activity = activitySource.StartActivity(name, ActivityKind.Client, default(ActivityContext));

            if (activity is null)
                return null;

            activity.SetTag(Tags.ConnectionString, connection.ConnectionString);
            activity.SetTag(Tags.DbName, connection.Database);
            return activity;
        }

        internal static void SetQuery(this Activity activity, string sql)
        {
            if (activity is null || sql is null)
                return;
            if (sql.Length > StatementMaxLen)
            {
                sql = sql.Substring(0, StatementMaxLen);
            }
            activity.SetTag(Tags.DbStatement, sql);
        }

        internal static void SetSuccess(this Activity activity)
        {
        #if NET6_0_OR_GREATER
            activity?.SetStatus(ActivityStatusCode.Ok);
        #endif
            activity?.SetTag(Tags.StatusCode, "OK");
            activity?.Stop();
        }

        internal static void SetException(this Activity activity, Exception exception)
        {
            if (exception is null) throw new ArgumentNullException(nameof(exception));

            var description = exception.Message;

            #if NET6_0_OR_GREATER
                activity?.SetStatus(ActivityStatusCode.Error, description);
            #endif

            activity?.SetTag(Tags.StatusCode, "ERROR");
            activity?.SetTag("otel.status_description", description);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", exception?.GetType().FullName },
                { "exception.message", exception?.Message },
            }));
            activity?.Stop();
        }


        private static ActivitySource CreateActivitySource()
        {
            var assembly = typeof(SnowflakeActivitySourceHelper).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            return new ActivitySource(ActivitySourceName, version);
        }
    }
}
