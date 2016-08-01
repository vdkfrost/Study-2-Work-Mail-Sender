using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S2W_Mail_Sender
{
    class Template
    {
        public string name, subject, text;
        public Template(string name, string subject, string text)
        {
            this.name = name;
            this.subject = subject;
            this.text = text;
        }
    }
}
