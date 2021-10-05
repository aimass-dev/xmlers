using System;
using System.Collections.Generic;
using System.Xml;

namespace xmlers
{
    //----------------------------------------------------------------------
    // XML関連
    //----------------------------------------------------------------------
    class Xml
    {
        //------------------------------------------------------------------
        // 定数
        //------------------------------------------------------------------
        private const string URL_SHA1 = "http://www.w3.org/2000/09/xmldsig#sha1";
        private const string URL_SHA256 = "http://www.w3.org/2001/04/xmlenc#sha256";
        private const string URL_SHA384 = "http://www.w3.org/2001/04/xmldsig-more#sha384";
        private const string URL_SHA512 = "http://www.w3.org/2001/04/xmlenc#sha512";
        private const string c_atsc = "*[local-name() = \"ArchiveTimeStampSequence\"]"
                            + "/*[local-name() = \"ArchiveTimeStampChain\"]";
        //------------------------------------------------------------------
        // ERSファイル読み込み
        //------------------------------------------------------------------
        internal static ErsinfoR read_ers(string fname, out string errstr)
        {
            errstr = null;
            XmlDocument doc = parse_xmlfile(fname, out errstr); // XMLファイル読み込み
            if (doc == null) return null;
            XmlNode root = doc.SelectSingleNode("*");
            if (root == null)
            {
                errstr = "ルート要素がありません。";
                return null;
            }
            if (root.LocalName != "EvidenceRecord")
            {
                errstr = "ルート要素がEvidenceRecordではありません。";
                return null;
            }
            XmlNode atsc = root.SelectSingleNode(c_atsc);
            if (atsc == null || ((XmlElement)atsc).GetAttribute("Order") != "1")
            {
                errstr = "ArchiveTimeStampChain Order1がありません。";
                return null;
            }
            string hashurl = getattr(atsc, "*[local-name() = \"DigestMethod\"]", "Algorithm");
            int hashidx = get_hashidx(hashurl);
            if (hashidx < 0)
            {
                errstr = "DigestMethod異常";
                return null;
            }
            XmlNode ats = atsc.SelectSingleNode("*[local-name() = \"ArchiveTimeStamp\"]");
            if (ats == null || ((XmlElement)ats).GetAttribute("Order") != "1")
            {
                errstr = "ArchiveTimeStamp Order1がありません。";
                return null;
            }
            try
            {
                string strtst = gettext(ats, "*[local-name() = \"TimeStamp\"]/*[local-name() = \"TimeStampToken\"]");
                if (strtst == null || strtst.Length == 0)
                {
                    errstr = "TimeStampTokenがありません。";
                    return null;
                }
                ErsinfoR ers = new ErsinfoR();
                ers.hashidx = hashidx;
                ers.btst = Convert.FromBase64String(strtst);
                XmlNode htree = ats.SelectSingleNode("*[local-name() = \"HashTree\"]");
                if (htree != null)
                {
                    Hashgrp[] hgrp = get_hashgrp(htree, out errstr); // ハッシュグループ取得
                    if (hgrp == null) return null;
                    ers.hashgrp = hgrp;
                }
                return ers;
            }
            catch (Exception e)
            {
                errstr = e.ToString();
                return null;
            }
        }
        //-----------------------------------------------------------
        // ハッシュグループ取得
        //-----------------------------------------------------------
        private static Hashgrp[] get_hashgrp(XmlNode htree, out string errstr)
        {
            errstr = null;
            XmlNodeList seqs = htree.SelectNodes("*[local-name() = \"Sequence\"]");
            int seqcnt = (seqs == null ? 0 : seqs.Count);
            if (seqcnt == 0)
            {
                errstr = "Sequenceがありません";
                return null;
            }
            List<Hashgrp> listhgrp = new List<Hashgrp>();
            for (int i = 0; i < seqcnt; i++)
            {
                XmlElement seq1 = (XmlElement)seqs[i];
                if (seq1.GetAttribute("Order") != "" + (i + 1))
                {
                    errstr = "Sequence Orderが異常です";
                    return null;
                }
                XmlNodeList dvals = seq1.SelectNodes("*[local-name() = \"DigestValue\"]/text()");
                if (dvals == null || dvals.Count == 0)
                {
                    errstr = "Sequence(Order=" + (i + 1) + ") DigestValueがありません";
                    return null;
                }
                List<byte[]> list = new List<byte[]>();
                foreach (XmlNode dval1 in dvals)
                {
                    list.Add(Convert.FromBase64String(dval1.Value));
                }
                listhgrp.Add(new Hashgrp(list.ToArray()));
            }
            return listhgrp.ToArray();
        }
        //-----------------------------------------------------------
        // ハッシュURL→index
        //-----------------------------------------------------------
        private static int get_hashidx(string url)
        {
            if (url == URL_SHA1) return 0;
            if (url == URL_SHA256) return 1;
            if (url == URL_SHA384) return 2;
            if (url == URL_SHA512) return 3;
            return -1;
        }
        /*******************
        //------------------------------------------------------------------
        internal class MyValidator
        {
            private string m_result = null;
            private string result() { return m_result; }
            private void ValidationEventHandler(object sender, System.Xml.Schema.ValidationEventArgs e)
            {
                m_result = e.Message;
            }
            internal static string validate(string fname)
            {
                MyValidator myv = new MyValidator();
                System.Xml.Schema.XmlSchemaSet schemas = new System.Xml.Schema.XmlSchemaSet();
                schemas.Add("urn:ietf:params:xml:ns:ers", "ers.xsd"); 
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.Schema;
                settings.Schemas = schemas;
                settings.ValidationEventHandler += myv.ValidationEventHandler;
                XmlReader reader = XmlReader.Create(fname, settings);
                while (reader.Read()) ; // empty body
                reader.Close(); // close reader stream 
                return myv.result();
            }
        }
        ********/
        //------------------------------------------------------------------
        // XMLファイル読み込み
        //------------------------------------------------------------------
        private static XmlDocument parse_xmlfile(string fname, out string errstr)
        {
            errstr = null;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = true; // 必須
                doc.Load(fname);
                return doc;
            }
            catch (Exception ex)
            {
                errstr = ex.Message;
                return null;
            }
        }
        //------------------------------------------------------------------
        // テキスト項目返却
        //------------------------------------------------------------------
        private static string gettext(XmlNode node, string name)
        {
            XmlNode node2 = node.SelectSingleNode(name + "/text()");
            if (node2 == null) return null;
            return node2.Value;
        }
        //------------------------------------------------------------------
        // 属性返却
        //------------------------------------------------------------------
        private static string getattr(XmlNode node, string elename, string attr)
        {
            XmlNode node2 = node.SelectSingleNode(elename);
            if (node2 == null) return null;
            if (node2.NodeType != XmlNodeType.Element) return null;
            return ((XmlElement)node2).GetAttribute(attr);
        }
    }
}
