using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon.Exception
{

    [Serializable]
    public class InvalidScriptException : System.Exception
    {
        public InvalidScriptException() { }
        public InvalidScriptException(string message) : base(message) { }
        public InvalidScriptException(string message, System.Exception inner) : base(message, inner) { }
        protected InvalidScriptException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
