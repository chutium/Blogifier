using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Helpers
{
    public class DIDResult
    {
        public string privateKey { get; set; }
        public string publicKey { get; set; }
        public string publicAddr { get; set; }
        public string did { get; set; }
    }
}
