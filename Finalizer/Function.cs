using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using LambdaSharp.Chat.Common.DataStore;
using LambdaSharp.Chat.Common.Records;
using LambdaSharp;
using LambdaSharp.Finalizer;

namespace LambdaSharp.Chat.Finalizer {

    public sealed class Function : ALambdaFinalizerFunction {

        //--- Fields ---
        private DataTable _dataTable;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            var dataTableName = config.ReadDynamoDBTableName("DataTable");

            // initialize AWS clients
            _dataTable = new DataTable(dataTableName, new AmazonDynamoDBClient());
        }

        public override Task CreateDeployment(FinalizerProperties current) {
            return CreateGeneralChannelAsync();
        }

        public override Task UpdateDeployment(FinalizerProperties next, FinalizerProperties previous) {
            return CreateGeneralChannelAsync();
        }

        private async Task CreateGeneralChannelAsync() {
            try {
                await _dataTable.CreateChannelAsync(new ChannelRecord {
                    ChannelId = "General",
                    ChannelName = "General"
                });
            } catch(AmazonDynamoDBException) {

                // ignore error
            }
        }
    }
}
