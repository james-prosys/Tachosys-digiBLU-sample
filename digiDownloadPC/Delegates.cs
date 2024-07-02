using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace digiDownloadPC
{
    public class DataEventArgs : EventArgs
    {
        /// <summary>
        /// Initialises a new instance of the DataEventArgs class using the specified values.
        /// </summary>
        public DataEventArgs(byte[] data)
        {
            this.Data = data;
        }

        /// <summary>
        /// Gets the data that has been received or sent.
        /// </summary>
        public byte[] Data { get; private set; }
    }

    public delegate void DataEventHandler(object sender, DataEventArgs e);

}
