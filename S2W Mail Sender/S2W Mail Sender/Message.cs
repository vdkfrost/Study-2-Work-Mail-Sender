using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S2W_Mail_Sender
{
    class Message
    {
        public int messageId;
        public bool error;
        public string type, messageSender, messageText, messageDate, receivers, attachmentsErrors, authorName, authorMail, groupId, labName;
        public List<string> attachments;
        public Message(int messageId, bool error, string type, string messageSender, string messageText, string messageDate, string receivers, string attachmentsErrors, string authorName, string authorMail, string groupId, string labName)
        {
            this.messageId = messageId;
            this.error = error;
            this.type = type;
            this.messageSender = messageSender;
            this.messageText = messageText;
            this.messageDate = messageDate;
            this.receivers = receivers;
            this.attachmentsErrors = attachmentsErrors;
            this.authorName = authorName;
            this.authorMail = authorMail;
            this.groupId = groupId;
            this.labName = labName;
            attachments = new List<string>();
        }
    }
}
