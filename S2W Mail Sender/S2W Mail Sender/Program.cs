using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Timers;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;

namespace S2W_Mail_Sender
{
    class Program
    {
        /* BLOCK: connections */
        public static string connectionString = ConfigurationManager.ConnectionStrings["baseConnectionString"].ConnectionString;
        public static SqlConnection connection = new SqlConnection(connectionString);
        public static SmtpClient smtpClient;

        /* BLOCK: application values */
        public static List<Template> messagesTemplates = new List<Template>();
        public static string serverLogin, serverPassword, smtpServerName;
        public static int smtpServerPort, timeout;

        public static string maxTextSize, maxAttachmentSize;
        static void Main(string[] args)
        {
            getInfoFromConfig(Directory.GetCurrentDirectory() + "//config.cfg");
            Console.SetWindowSize(120, 40);

            FileInfo[] files = new DirectoryInfo(Directory.GetCurrentDirectory() + "//Message templates").GetFiles("*.html");
            foreach (FileInfo file in files)
            {
                FileStream template = new FileStream(file.Directory.FullName + "\\" + file.Name, FileMode.Open, FileAccess.Read);
                StreamReader templateReader = new StreamReader(template, Encoding.UTF8);
                List<string> content = new List<string>();
                content.AddRange(templateReader.ReadToEnd().Split('\n').ToList());
                for (int i = 0; i < content.Count; i++)
                {
                    string line = formatMessage(content[i]);
                    if (line == " " || line == "")
                    {
                        content.RemoveAt(i);
                        i--;
                    }
                }
                string subject = content[0].Substring(4, content[0].Length - 8);
                string text = "";
                for (int i = 1; i < content.Count; i++)
                    text += formatMessage(content[i]);

                messagesTemplates.Add(new Template(file.Name.Remove(file.Name.Length - 5), subject, text));
            }

            System.Timers.Timer checkNewMessagesTimer = new System.Timers.Timer(timeout);

            smtpClient = new SmtpClient(smtpServerName, smtpServerPort);
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new System.Net.NetworkCredential(serverLogin, serverPassword);
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.EnableSsl = true;

            checkNewMessages(checkNewMessagesTimer, null);
            checkNewMessagesTimer.Elapsed += new ElapsedEventHandler(checkNewMessages);
            Console.ReadLine();
        }
        public static void checkNewMessages(object sender, ElapsedEventArgs e)
        {
            (sender as System.Timers.Timer).Stop();
            Console.WriteLine(DateTime.Now.ToString() + " - Запускаю поиск новых сообщений для отправки в базе");
            int countOfMessages = 0;
            List<Message> messages = new List<Message>();
            List<Attachment> attachments = new List<Attachment>();

            connection.Open();
            SqlCommand findAttachments = new SqlCommand("EXEC GetAttachmentsAndDelete", connection);
            SqlDataReader findAttachmentsReader = findAttachments.ExecuteReader();
            while (findAttachmentsReader.Read())
                attachments.Add(new Attachment(findAttachmentsReader.GetInt32(1), findAttachmentsReader.GetString(2)));
            connection.Close();

            connection.Open();
            SqlCommand findNewMsgs = new SqlCommand("EXEC GetMessagesAndDelete", connection);
            SqlDataReader fnmgr = findNewMsgs.ExecuteReader();
            while (fnmgr.Read())
            {
                countOfMessages++;
                messages.Add(new Message(fnmgr.GetInt32(0), fnmgr.GetBoolean(1), fnmgr.GetString(2), fnmgr.GetString(3), fnmgr.GetString(4), fnmgr.GetString(5),
                    fnmgr.GetString(6), fnmgr.GetString(7), fnmgr.GetString(8), fnmgr.GetString(9), fnmgr.GetString(2) == "new comment" ? fnmgr.GetInt32(10).ToString() : null, fnmgr.GetString(11)));
            }
            connection.Close();

            foreach (Attachment attach in attachments)
                for (int i = 0; i < messages.Count; i++)
                    if (messages[i].messageId == attach.messageId)
                    {
                        messages[i].attachments.Add(attach.path);
                        break;
                    }

            foreach (Message m in messages)
                newMessage(m.messageId, m.error, m.type, m.messageSender, m.messageText, m.messageDate, m.receivers, m.attachmentsErrors, m.authorName, m.authorMail, m.groupId, m.labName, m.attachments);

            if (countOfMessages == 0)
                Console.WriteLine(DateTime.Now.ToString() + " - Количество найденных сообщений: " + countOfMessages.ToString() + "\n");
            

            (sender as System.Timers.Timer).Start();
        }

        public static void newMessage(int messageId, bool error, string type, string messageSender, string messageText, string messageDate, string receivers, string attachmentsErrors, string authorName, string authorMail, string groupId, string labName, List<string> attachments)
        {
            MailMessage mail = new MailMessage();
            mail.From = new System.Net.Mail.MailAddress("noreply.s2w@gmail.com", "Уведомитель Study 2 Work");
            mail.IsBodyHtml = true;
            mail.SubjectEncoding = Encoding.UTF8;

            Template needed = null;
            foreach (Template temp in messagesTemplates)
                if (temp.name == type)
                {
                    needed = temp;
                    break;
                }

            switch (needed.name)
            {
                case "new comment":
                    mail.Subject = needed.subject.Replace("[!@#groupId]", groupId).Replace("[!@#labName]", labName);
                    mail.Body = needed.text.Replace("[!@#userName]", authorName).Replace("[!@#userText]", messageText);

                    connection.Open();
                    SqlCommand getAttachments = new SqlCommand("SELECT * FROM [dbo].[mailAttachments] WHERE [mailMessageId] = '" + messageId.ToString() + "'", connection);
                    SqlDataReader getAttachmentsReader = getAttachments.ExecuteReader();
                    while (getAttachmentsReader.Read())
                        mail.Attachments.Add(new System.Net.Mail.Attachment(getAttachmentsReader.GetString(2).ToString()));
                    connection.Close();

                    break;
                case "access error":
                    mail.Subject = needed.subject;
                    mail.Body = needed.text.Replace("[!@#userName]", authorName).Replace("[!@#userText]", messageText);
                    break;
                case "attachment size error":
                    mail.Subject = string.Format(needed.subject, groupId, labName);
                    mail.Body = needed.text.Replace("[!@#userName]", authorName).Replace("[!@#attachmentsErrors]", attachmentsErrors).Replace("[!@#maxAttachmentSize]", maxAttachmentSize);
                    break;
                case "empty message error":
                    mail.Subject = needed.subject;
                    mail.Body = needed.text.Replace("[!@#userName]", authorName);
                    break;
                case "for admin":
                    mail.Subject = needed.subject;
                    mail.Body = needed.text.Replace("[!@#countOfMessages]", messageText);
                    break;
                case "text size error":
                    mail.Subject = needed.subject;
                    mail.Body = needed.text.Replace("[!@#userName]", authorName).Replace("[!@#maxTextSize]", maxTextSize).Replace("[!@#maxAttachmentSize]", maxAttachmentSize);
                    break;
                case "subject error":
                    mail.Subject = needed.subject;
                    mail.Body = needed.text.Replace("[!@#userName]", authorName).Replace("[!@#userText]", messageText);
                    break;
            }

            foreach (string receiver in receivers.Split(' '))
            {
                mail.To.Add(new MailAddress(receiver));
                WriteLine(string.Format("- Отправил сообщение типа {0} пользователю {1}", type, receiver), false);
            }

            Console.WriteLine();
            smtpClient.Send(mail);
        }

        /* BLOCK: service methods */
        public static int countOfInputs(string text)
        {
            int count = 0;
            bool side = false;
            for (int i = text.IndexOf('{'); i < text.Length && i > 0; i++)
            {
                if (!side)
                {
                    if (text[i] == '{')
                    {
                        count++;
                        side = true;
                    }
                }
                else
                {
                    if (text[i] == '}')
                        side = false;
                }
            }
            return count;
        }
        public static void WriteLine(string text, bool makeNewLine)
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine(DateTime.Now.ToString() + " " + text);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            if (makeNewLine)
                Console.WriteLine();
        }
        public static string formatMessage(string message)
        {
            if (message.IndexOf("Уведомитель Study 2 Work <noreply.s2w@gmail.com>") != -1)
                message = message.Substring(0, message.IndexOf("Уведомитель Study 2 Work <noreply.s2w@gmail.com>"));
            string[] appleDevices = new string[] { "Отправлено с iPhone", "Отправлено с iPad" };
            foreach (string device in appleDevices)
                if (message.LastIndexOf(device) != -1)
                    message = message.Substring(0, message.LastIndexOf(device));
            if (message.LastIndexOf('\n') != -1)
                message = message.Substring(0, message.LastIndexOf('\n') + 1);
            message = message.Replace("\r\n", " ").Replace("\t", "").Replace("\n", " ");
            for (int i = 0; i < message.Length - 1; i++)
                if (message[i] == ' ' && message[i + 1] == ' ')
                {
                    message = message.Remove(i + 1, 1);
                    i--;
                }
            message = message.Replace("\r", "");
            return message;
        }

        public static void getInfoFromConfig(string configPath)
        {
            List<Setting> configData = new List<Setting>();
            FileStream config = new FileStream(configPath, FileMode.Open, FileAccess.Read);
            StreamReader configReader = new StreamReader(config, Encoding.UTF8);
            foreach (string line in configReader.ReadToEnd().Replace("\r\n", "•").Split('•'))
                if (formatMessage(line) != "" && formatMessage(line) != " ")
                {
                    string[] splittedLine = line.Split('=');
                    if (splittedLine.Length != 1)
                        configData.Add(new Setting(splittedLine[0], splittedLine[1]));
                    else
                        configData.Add(new Setting(line, null));
                }
            string objectOptionSwitcher = "";

            foreach (Setting set in configData)
            {
                if (set.option.IndexOf('[') != -1)
                    objectOptionSwitcher = set.option;
                else
                    switch (objectOptionSwitcher)
                    {
                        case "[SMTP SERVER]":
                            switch (set.option)
                            {
                                case "name":
                                    smtpServerName = set.value;
                                    break;
                                case "port":
                                    smtpServerPort = Convert.ToInt32(set.value);
                                    break;
                                case "login":
                                    serverLogin = set.value;
                                    break;
                                case "password":
                                    serverPassword = set.value;
                                    break;
                            }
                            break;
                        case "[APP]":
                            switch (set.option)
                            {
                                case "timeout":
                                    timeout = Convert.ToInt32(set.value);
                                    break;
                                case "maxTextSize":
                                    maxTextSize = set.value;
                                    break;
                                case "maxAttachmentSize":
                                    maxAttachmentSize = set.value;
                                    break;
                            }
                            break;
                    }
            }
        }
    }
}
