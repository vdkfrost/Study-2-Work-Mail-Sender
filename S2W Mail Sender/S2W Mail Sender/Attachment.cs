using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S2W_Mail_Sender
{
    class Attachment
    {
        public int messageId;
        public string path;
        public Attachment(int messageId, string path)
        {
            this.messageId = messageId;
            this.path = path;
        }
    }
}
