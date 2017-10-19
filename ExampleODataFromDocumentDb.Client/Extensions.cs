using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleODataFromDocumentDb.Client
{
    public partial class House
    {
        /// <summary>
        /// On construction, set DocumentType - on the private backing variable to avoid confusing the data service client
        /// </summary>
        public House()
        {
            if(this._DocumentType == null)
            {
                if (this is HouseHistory)
                    this._DocumentType = Client.DocumentType.HouseHistory;
                else
                    this._DocumentType = Client.DocumentType.House;
            }
        }
    }
}
