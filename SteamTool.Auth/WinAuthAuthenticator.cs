﻿using SteamTool.Auth.Win32;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Xml;
using WinAuth;

namespace SteamTool.Auth
{
    public interface IWinAuthAuthenticatorChangedListener
    {
        void OnWinAuthAuthenticatorChanged(WinAuthAuthenticator sender, WinAuthAuthenticatorChangedEventArgs e);
    }

    /// <summary>
    /// Wrapper for real authenticator data used to save to file with other application information
    /// </summary>
    public class WinAuthAuthenticator : ICloneable
    {
        /// <summary>
        /// Event handler fired when property is changed
        /// </summary>
        public event WinAuthAuthenticatorChangedHandler OnWinAuthAuthenticatorChanged;

        /// <summary>
        /// Unique Id of authenticator saved in config
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Index for authenticator when in sorted list
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Actual authenticator data
        /// </summary>
        public Authenticator AuthenticatorData { get; set; }

        /// <summary>
        /// When this authenticator was created
        /// </summary>
        public DateTime Created { get; set; }

        private string _name;
        private bool _autoRefresh;
        private bool _allowCopy;
        private bool _copyOnCode;
        private bool _hideSerial;

        /// <summary>
        /// Create the authenticator wrapper
        /// </summary>
        public WinAuthAuthenticator()
        {
            Id = Guid.NewGuid();
            Created = DateTime.Now;
            _autoRefresh = true;
        }

        /// <summary>
        /// Clone this authenticator
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            WinAuthAuthenticator clone = this.MemberwiseClone() as WinAuthAuthenticator;

            clone.Id = Guid.NewGuid();
            clone.OnWinAuthAuthenticatorChanged = null;
            clone.AuthenticatorData = (this.AuthenticatorData != null ? this.AuthenticatorData.Clone() as Authenticator : null);

            return clone;
        }

        /// <summary>
        /// Mark this authenticator as having changed
        /// </summary>
        public void MarkChanged()
        {
            if (OnWinAuthAuthenticatorChanged != null)
            {
                OnWinAuthAuthenticatorChanged(this, new WinAuthAuthenticatorChangedEventArgs());
            }
        }

        /// <summary>
        /// Get/set the name of this authenticator
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                if (OnWinAuthAuthenticatorChanged != null)
                {
                    OnWinAuthAuthenticatorChanged(this, new WinAuthAuthenticatorChangedEventArgs("Name"));
                }
            }
        }

        /// <summary>
        /// Get/set auto refresh flag
        /// </summary>
        public bool AutoRefresh
        {
            get
            {
                if (this.AuthenticatorData != null && this.AuthenticatorData is HOTPAuthenticator)
                {
                    return false;
                }
                else
                {
                    return _autoRefresh;
                }
            }
            set
            {
                // HTOP must always be false
                if (this.AuthenticatorData != null && this.AuthenticatorData is HOTPAuthenticator)
                {
                    _autoRefresh = false;
                }
                else
                {
                    _autoRefresh = value;
                }
                if (OnWinAuthAuthenticatorChanged != null)
                {
                    OnWinAuthAuthenticatorChanged(this, new WinAuthAuthenticatorChangedEventArgs("AutoRefresh"));
                }
            }
        }

        /// <summary>
        /// Get/set allow copy flag
        /// </summary>
        public bool AllowCopy
        {
            get
            {
                return _allowCopy;
            }
            set
            {
                _allowCopy = value;
                if (OnWinAuthAuthenticatorChanged != null)
                {
                    OnWinAuthAuthenticatorChanged(this, new WinAuthAuthenticatorChangedEventArgs("AllowCopy"));
                }
            }
        }

        /// <summary>
        /// Get/set auto copy flag
        /// </summary>
        public bool CopyOnCode
        {
            get
            {
                return _copyOnCode;
            }
            set
            {
                _copyOnCode = value;
                if (OnWinAuthAuthenticatorChanged != null)
                {
                    OnWinAuthAuthenticatorChanged(this, new WinAuthAuthenticatorChangedEventArgs("CopyOnCode"));
                }
            }
        }

        /// <summary>
        /// Get/set hide serial flag
        /// </summary>
        public bool HideSerial
        {
            get
            {
                return _hideSerial;
            }
            set
            {
                _hideSerial = value;
                if (OnWinAuthAuthenticatorChanged != null)
                {
                    OnWinAuthAuthenticatorChanged(this, new WinAuthAuthenticatorChangedEventArgs("HideSerial"));
                }
            }
        }


        public string CurrentCode
        {
            get
            {
                if (this.AuthenticatorData == null)
                {
                    return null;
                }

                string code = this.AuthenticatorData.CurrentCode;

                if (this.AuthenticatorData is HOTPAuthenticator)
                {
                    if (OnWinAuthAuthenticatorChanged != null)
                    {
                        OnWinAuthAuthenticatorChanged(this, new WinAuthAuthenticatorChangedEventArgs("HOTP", this.AuthenticatorData));
                    }
                }

                return code;
            }
        }

        /// <summary>
        /// Sync the current authenticator's time with its server
        /// </summary>
        public void Sync()
        {
            if (AuthenticatorData != null)
            {
                try
                {
                    AuthenticatorData.Sync();
                }
                catch (EncryptedSecretDataException)
                {
                    // reset lastsync to force sync on next decryption
                }
            }
        }

        /// <summary>
        /// Copy the current code to the clipboard
        /// </summary>
        public void CopyCodeToClipboard(string code = null, bool showError = false)
        {
            if (code == null)
            {
                code = this.CurrentCode;
            }

            bool clipRetry = false;
            do
            {
                bool failed = false;
                // check if the clipboard is locked
                IntPtr hWnd = WinAPI.GetOpenClipboardWindow();
                if (hWnd != IntPtr.Zero)
                {
                    int len = WinAPI.GetWindowTextLength(hWnd);
                    if (len == 0)
                    {
                        //WinAuthMain.LogException(new ApplicationException("Clipboard in use by another process"));
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder(len + 1);
                        WinAPI.GetWindowText(hWnd, sb, sb.Capacity);
                        //WinAuthMain.LogException(new ApplicationException("Clipboard in use by '" + sb.ToString() + "'"));
                    }

                    failed = true;
                }
                else
                {
                    // Issue#170: can still get error copying even though it works, so just increase retries and ignore error
                    try
                    {
                        Clipboard.Clear();

                        // add delay for clip error
                        System.Threading.Thread.Sleep(100);
                        Clipboard.SetDataObject(code, true);
                    }
                    catch (ExternalException)
                    {
                    }
                }

                if (failed == true && showError == true)
                {
                    // only show an error the first time
                    //clipRetry = (MessageBox.Show(form, strings.ClipboardInUse,
                    //    WinAuthMain.APPLICATION_NAME,
                    //    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes);

                }
            }
            while (clipRetry == true);
        }

        public bool ReadXml(XmlReader reader, string password)
        {
            bool changed = false;

            Guid id;
            if (Guid.TryParse(reader.GetAttribute("id"), out id) == true)
            {
                Id = id;
            }

            string authenticatorType = reader.GetAttribute("type");
            if (string.IsNullOrEmpty(authenticatorType) == false)
            {
                Type type = typeof(Authenticator).Assembly.GetType(authenticatorType, false, true);
                this.AuthenticatorData = Activator.CreateInstance(type) as Authenticator;
            }

            //string encrypted = reader.GetAttribute("encrypted");
            //if (string.IsNullOrEmpty(encrypted) == false)
            //{
            //	// read the encrypted text from the node
            //	string data = reader.ReadElementContentAsString();
            //	// decrypt
            //	Authenticator.PasswordTypes passwordType;
            //	data = Authenticator.DecryptSequence(data, encrypted, password, out passwordType);

            //	using (MemoryStream ms = new MemoryStream(Authenticator.StringToByteArray(data)))
            //	{
            //		reader = XmlReader.Create(ms);
            //		ReadXml(reader, password);
            //	}
            //	this.PasswordType = passwordType;
            //	this.Password = password;

            //	return;
            //}

            reader.MoveToContent();

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return changed;
            }

            reader.Read();
            while (reader.EOF == false)
            {
                if (reader.IsStartElement())
                {
                    switch (reader.Name)
                    {
                        case "name":
                            Name = reader.ReadElementContentAsString();
                            break;

                        case "created":
                            long t = reader.ReadElementContentAsLong();
                            t += Convert.ToInt64(new TimeSpan(new DateTime(1970, 1, 1).Ticks).TotalMilliseconds);
                            t *= TimeSpan.TicksPerMillisecond;
                            Created = new DateTime(t).ToLocalTime();
                            break;

                        case "autorefresh":
                            _autoRefresh = reader.ReadElementContentAsBoolean();
                            break;

                        case "allowcopy":
                            _allowCopy = reader.ReadElementContentAsBoolean();
                            break;

                        case "copyoncode":
                            _copyOnCode = reader.ReadElementContentAsBoolean();
                            break;

                        case "hideserial":
                            _hideSerial = reader.ReadElementContentAsBoolean();
                            break;

                        case "authenticatordata":
                            try
                            {
                                // we don't pass the password as they are locked till clicked
                                changed = this.AuthenticatorData.ReadXml(reader) || changed;
                            }
                            catch (EncryptedSecretDataException)
                            {
                                // no action needed
                            }
                            catch (BadPasswordException)
                            {
                                // no action needed
                            }
                            break;

                        // v2
                        case "authenticator":
                            this.AuthenticatorData = Authenticator.ReadXmlv2(reader, password);
                            break;
                        // v2
                        case "servertimediff":
                            this.AuthenticatorData.ServerTimeDiff = reader.ReadElementContentAsLong();
                            break;


                        default:
                            reader.Skip();
                            break;
                    }
                }
                else
                {
                    reader.Read();
                    break;
                }
            }

            return changed;
        }

        /// <summary>
        /// Write the data as xml into an XmlWriter
        /// </summary>
        /// <param name="writer">XmlWriter to write config</param>
        public void WriteXmlString(XmlWriter writer)
        {
            writer.WriteStartElement(typeof(WinAuthAuthenticator).Name);
            writer.WriteAttributeString("id", this.Id.ToString());
            if (this.AuthenticatorData != null)
            {
                writer.WriteAttributeString("type", this.AuthenticatorData.GetType().FullName);
            }

            //if (this.PasswordType != Authenticator.PasswordTypes.None)
            //{
            //	string data;

            //	using (MemoryStream ms = new MemoryStream())
            //	{
            //		XmlWriterSettings settings = new XmlWriterSettings();
            //		settings.Indent = true;
            //		settings.Encoding = Encoding.UTF8;
            //		using (XmlWriter encryptedwriter = XmlWriter.Create(ms, settings))
            //		{
            //			Authenticator.PasswordTypes savedpasswordType = PasswordType;
            //			PasswordType = Authenticator.PasswordTypes.None;
            //			WriteXmlString(encryptedwriter);
            //			PasswordType = savedpasswordType;
            //		}
            //		//data = Encoding.UTF8.GetString(ms.ToArray());
            //		data = Authenticator.ByteArrayToString(ms.ToArray());
            //	}

            //	string encryptedTypes;
            //	data = Authenticator.EncryptSequence(data, PasswordType, Password, out encryptedTypes);
            //	writer.WriteAttributeString("encrypted", encryptedTypes);
            //	writer.WriteString(data);
            //	writer.WriteEndElement();

            //	return;
            //}

            writer.WriteStartElement("name");
            writer.WriteValue(this.Name ?? string.Empty);
            writer.WriteEndElement();

            writer.WriteStartElement("created");
            writer.WriteValue(Convert.ToInt64((this.Created.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalMilliseconds));
            writer.WriteEndElement();

            writer.WriteStartElement("autorefresh");
            writer.WriteValue(this.AutoRefresh);
            writer.WriteEndElement();
            //
            writer.WriteStartElement("allowcopy");
            writer.WriteValue(this.AllowCopy);
            writer.WriteEndElement();
            //
            writer.WriteStartElement("copyoncode");
            writer.WriteValue(this.CopyOnCode);
            writer.WriteEndElement();
            //
            writer.WriteStartElement("hideserial");
            writer.WriteValue(this.HideSerial);
            writer.WriteEndElement();
            //

            // save the authenticator to the config file
            if (this.AuthenticatorData != null)
            {
                this.AuthenticatorData.WriteToWriter(writer);

                // save script with password and generated salt
                //if (this.AutoLogin != null)
                //{
                //	this.AutoLogin.WriteXmlString(writer, this.AuthenticatorData.PasswordType, this.AuthenticatorData.Password);
                //}
            }

            writer.WriteEndElement();
        }

        /// <summary>
        /// Create a KeyUriFormat compatible URL
        /// See https://code.google.com/p/google-authenticator/wiki/KeyUriFormat
        /// </summary>
        /// <returns>string</returns>
        public virtual string ToUrl(bool compat = false)
        {
            string type = "totp";
            string extraparams = string.Empty;

            Match match;
            string issuer = this.AuthenticatorData.Issuer;
            string label = this.Name;
            if (string.IsNullOrEmpty(issuer) == true && (match = Regex.Match(label, @"^([^\(]+)\s+\((.*?)\)(.*)")).Success == true)
            {
                issuer = match.Groups[1].Value;
                label = match.Groups[2].Value + match.Groups[3].Value;
            }
            if (string.IsNullOrEmpty(issuer) == false && (match = Regex.Match(label, @"^" + issuer + @"\s+\((.*?)\)(.*)")).Success == true)
            {
                label = match.Groups[1].Value + match.Groups[2].Value;
            }
            if (string.IsNullOrEmpty(issuer) == false)
            {
                extraparams += "&issuer=" + HttpUtility.UrlEncode(issuer);
            }

            if (this.AuthenticatorData.HMACType != Authenticator.DEFAULT_HMAC_TYPE)
            {
                extraparams += "&algorithm=" + this.AuthenticatorData.HMACType.ToString();
            }

            if (this.AuthenticatorData is BattleNetAuthenticator)
            {
                extraparams += "&serial=" + HttpUtility.UrlEncode(((BattleNetAuthenticator)this.AuthenticatorData).Serial.Replace("-", ""));
            }
            else if (this.AuthenticatorData is SteamAuthenticator)
            {
                if (compat == false)
                {
                    extraparams += "&deviceid=" + HttpUtility.UrlEncode(((SteamAuthenticator)this.AuthenticatorData).DeviceId);
                    extraparams += "&data=" + HttpUtility.UrlEncode(((SteamAuthenticator)this.AuthenticatorData).SteamData);
                }
            }
            else if (this.AuthenticatorData is HOTPAuthenticator)
            {
                type = "hotp";
                extraparams += "&counter=" + ((HOTPAuthenticator)this.AuthenticatorData).Counter;
            }

            string secret = HttpUtility.UrlEncode(Base32.getInstance().Encode(this.AuthenticatorData.SecretKey));

            if (this.AuthenticatorData.Period != Authenticator.DEFAULT_PERIOD)
            {
                extraparams += "&period=" + this.AuthenticatorData.Period;
            }

            var url = string.Format("otpauth://" + type + "/{0}?secret={1}&digits={2}{3}",
              (string.IsNullOrEmpty(issuer) == false ? HttpUtility.UrlPathEncode(issuer) + ":" + HttpUtility.UrlPathEncode(label) : HttpUtility.UrlPathEncode(label)),
              secret,
              this.AuthenticatorData.CodeDigits,
              extraparams);

            return url;
        }

    }

    /// <summary>
    /// Delegate for ConfigChange event
    /// </summary>
    /// <param name="source"></param>
    /// <param name="args"></param>
    public delegate void WinAuthAuthenticatorChangedHandler(WinAuthAuthenticator source, WinAuthAuthenticatorChangedEventArgs args);

    /// <summary>
    /// Change event arguments
    /// </summary>
    public class WinAuthAuthenticatorChangedEventArgs : EventArgs
    {
        public string Property { get; private set; }
        public Authenticator Authenticator { get; private set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public WinAuthAuthenticatorChangedEventArgs(string property = null, Authenticator authenticator = null)
        {
            Property = property;
            Authenticator = authenticator;
        }

    }
}
