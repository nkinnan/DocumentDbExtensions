using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace DocumentDbExtensionsTest
{
    public class DocumentDbHelper
    {
        internal static StoredProcedure updateAndRecalculateDetailedStatusStoredProcedure = new StoredProcedure()
        {
            Id = "UpdateAndRecalculateDetailedStatus",
            Body = ExtractResource(@"DocumentDbExtensionsTest.UpdateAndRecalculateDetailedStatus.ts"),
        };

        #region helpers
        private static string ExtractResource(string filename)
        {
            System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
            using (Stream resFilestream = a.GetManifestResourceStream(filename))
            {
                if (resFilestream == null)
                {
                    throw new InvalidOperationException("Resource not found: " + filename);
                }
                byte[] ba = new byte[resFilestream.Length];
                resFilestream.Read(ba, 0, ba.Length);
                return Encoding.UTF8.GetString(ba);
            }
        }

        private static async Task<Database> GetOrCreateDatabase(DocumentClient client, string databaseName)
        {
            Database database = client.CreateDatabaseQuery()
                                      .Where(x => x.Id == databaseName)
                                      .AsEnumerable()
                                      .FirstOrDefault();
            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new Database() { Id = databaseName });
            }

            return database;
        }

        private static async Task<DocumentCollection> GetOrCreateCollection(DocumentClient client, Database database, string collectionName)
        {
            var collection = client.CreateDocumentCollectionQuery("dbs/" + database.Id)
                                   .Where(c => c.Id == collectionName)
                                   .AsEnumerable()
                                   .FirstOrDefault();
            if (collection == null)
            {
                // without explicitly doing a range index on both number and string types, you will not be able to do "Order By" in a 
                // lot of cases and it will just return no results with a continuation token 
                collection = new DocumentCollection() { Id = collectionName };
                collection.IndexingPolicy.IncludedPaths.Add(
                    new IncludedPath
                    {
                        Path = "/*",
                        Indexes = new Collection<Index>
                        {
                            new RangeIndex(DataType.String) { Precision = -1 },
                            new RangeIndex(DataType.Number) { Precision = -1 },
                        }
                    });

                collection = await client.CreateDocumentCollectionAsync(
                    "dbs/" + database.Id,
                    collection);
            }

            return collection;
        }
        #endregion

        public static async Task<DocumentClient> GetDocumentClient(string connectionString, string databaseName, string collectionName)
        {
            string uriString = null;
            string authKey = null;
            string[] pieces = connectionString.Split(";".ToArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (string piece in pieces)
            {
                string[] subpieces = piece.Split("=".ToArray());
                if (subpieces[0] == "AccountEndpoint")
                {
                    uriString = subpieces[1];
                }
                if (subpieces[0] == "AccountKey")
                {
                    authKey = subpieces[1];
                }
            }

            Uri serviceEndpoint = new Uri(uriString);

            var client = new DocumentClient(serviceEndpoint,
                                            authKey,
                                            new ConnectionPolicy
                                            {
                                                ConnectionMode = ConnectionMode.Direct,
                                                //ConnectionProtocol = Protocol.Tcp,
                                                RetryOptions = new RetryOptions()
                                                {
                                                    MaxRetryAttemptsOnThrottledRequests = 0
                                                }
                                            },
                                            ConsistencyLevel.Session);

            string collectionLink = string.Format("dbs/{0}/colls/{1}", databaseName, collectionName);

            var database = await DocumentDbExtensions.ExecuteMethodWithRetryAsync(() =>
                GetOrCreateDatabase(client, databaseName));

            var collection = await DocumentDbExtensions.ExecuteMethodWithRetryAsync(() =>
                GetOrCreateCollection(client, database, collectionName));

            var response = await DocumentDbExtensions.ExecuteMethodWithRetryAsync(() =>
                client.UpsertStoredProcedureAsync(collectionLink, updateAndRecalculateDetailedStatusStoredProcedure));

            return client;
        }
    }
}