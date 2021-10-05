using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace xmlers
{
    //----------------------------------------------------------------------
    // ASN.1
    //----------------------------------------------------------------------
    static class Asn1
    {
        //-----------------------------------------------------------
        // タグ定数
        //-----------------------------------------------------------
        const byte T_BOOL = 0x01;
        const byte T_INT = 0x02;
        const byte T_OCTET = 0x04;
        const byte T_NULL = 0x05;
        const byte T_OID = 0x06;
        const byte T_UTF8 = 0x0c;
        const byte T_GTIME = 0x18;
        const byte T_SEQ = 0x30;
        const byte T_SET = 0x31;
        const byte T_CTX0 = (byte)0xa0;
        const byte T_CTX1 = (byte)0xa1;
        //----------------------------------------------------------------------
        // タイムスタンプ情報取得
        //----------------------------------------------------------------------
        static internal Tstinfo get_tstinfo(byte[] bdata, out string errstr)
        {
            errstr = null;
            try
            {
                int len = bdata.Length;
                int pos = 0;
                if (bdata[pos] != T_SEQ)
                {
                    errstr = "TST not SEQUENCE";
                    return null;
                }
                if (!get_asn1_child(bdata, pos, len, 1, T_CTX0, out pos, out len)) return null; // [0](CMS explicit)
                if (!get_asn1_child(bdata, pos, len, 0, T_SEQ, out pos, out len)) return null; // SEQ(CMSsigned top)
                int pos_cst = pos;
                int len_cst = len;

                // INT,SET,SEQ,opt[0],opt[1],SET
                if (!get_asn1_child(bdata, pos, len, 2, T_SEQ, out pos, out len)) return null; // SEQ(Content)
                if (!get_asn1_child(bdata, pos, len, 1, T_CTX0, out pos, out len)) return null; // [0](Content of spcOID)
                if (!get_asn1_child(bdata, pos, len, 0, T_OCTET, out pos, out len)) return null; // OCTETSTRING
                if (!get_asn1_child(bdata, pos, len, 0, T_SEQ, out pos, out len)) return null; // SEQ(TSTInfo)

                // INT,OID,SEQ,INT,GenTime, ... [0]
                int pos2, len2, lenlen, datalen;
                if (!get_asn1_child(bdata, pos, len, 2, T_SEQ, out pos2, out len2)) return null; // SEQ(hash)
                if (!get_asn1_child(bdata, pos2, len2, 1, T_OCTET, out pos2, out len2)) return null; // hashval
                get_asn1_tl(bdata, pos2, out lenlen, out datalen);
                byte[] bhash = new byte[datalen];
                Array.Copy(bdata, pos2 + 1 + lenlen, bhash, 0, datalen);
                if (!get_asn1_child(bdata, pos, len, 4, T_GTIME, out pos2, out len2)) return null; // GENTIME
                get_asn1_tl(bdata, pos2, out lenlen, out datalen);
                string timestr = Encoding.UTF8.GetString(bdata, pos2 + 1 + lenlen, datalen);

                if (!get_asn1_child(bdata, pos_cst, len_cst, 3, T_CTX0, out pos, out len)) return null; // [0](certs)
                List<byte[]> certs = new List<byte[]>();
                if (!get_seqs_in_set(bdata, pos, len, certs)) return null;

                Tstinfo ret = new Tstinfo();
                ret.bhash = bhash;
                ret.timestr = fmt_asn1time(timestr);
                ret.certs = certs.ToArray();
                return ret;
            }
            catch (Exception ex)
            {
                errstr = ex.ToString();
                return null;
            }
        }
        //----------------------------------------------------------------
        // SET内のSEQUENCEを取り出す
        //----------------------------------------------------------------
        static public bool get_seqs_in_set(byte[] bsrc, int stpos, int len, List<byte[]> seqs)
        {
            seqs.Clear();
            int[] innerpos;
            if (!get_asn1_inner(bsrc, stpos, len, out innerpos)) return false;
            int itemcnt = innerpos.Length;
            for (int i = 0; i < itemcnt; i++)
            {
                int pos1 = innerpos[i];
                if (bsrc[pos1] != T_SEQ) continue;  // SEQUENCE以外：TACなど
                int pos_end = (i < itemcnt - 1 ? innerpos[i + 1] : stpos + len);
                int len1 = pos_end - pos1;
                byte[] barr = new byte[len1];
                //for(int k = 0; k < len1; k++)  barr[i] = bsrc[stpos+k];
                Array.Copy(bsrc, pos1, barr, 0, len1);
                seqs.Add(barr);
            }
            return true;
        }
        //----------------------------------------------------------------
        // 子要素取り出し
        //----------------------------------------------------------------
        static private bool get_asn1_child(byte[] bsrc, int pos, int len, int childidx, byte reqtag, out int childpos, out int childlen)
        {
            childpos = childlen = 0;
            int[] innerpos;
            if (!get_asn1_inner(bsrc, pos, len, out innerpos)) return false;
            int itemcnt = innerpos.Length;
            if (itemcnt == 0 || childidx >= itemcnt) return false;
            if (childidx < 0) childidx = itemcnt - 1;
            childpos = innerpos[childidx];
            if (bsrc[childpos] != reqtag) return false;
            int child_end = (childidx == itemcnt - 1 ? pos + len : innerpos[childidx + 1]);
            childlen = child_end - childpos;
            return true;
        }
        //----------------------------------------------------------------
        // ASN.1内部分解
        //----------------------------------------------------------------
        static private bool get_asn1_inner(byte[] bsrc, int stpos, int len, out int[] innerpos)
        {
            int lenlen, datalen;
            innerpos = null;
            if (!get_asn1_tl(bsrc, stpos, out lenlen, out datalen)) return false;
            if (1 + lenlen + datalen != len) return false;
            int pos = stpos + 1 + lenlen;
            int enpos = stpos + len;
            List<int> list = new List<int>();
            while (true)
            {
                if (pos + 1 >= enpos) return false;
                if (!get_asn1_tl(bsrc, pos, out lenlen, out datalen)) return false;
                int curlen = 1 + lenlen + datalen;
                if (pos + curlen > enpos) return false;
                list.Add(pos);
                pos += curlen;
                if (pos == enpos) break;
            }
            innerpos = list.ToArray();
            return true;
        }
        //----------------------------------------------------------------
        // ASN.1タグ長取得
        //----------------------------------------------------------------
        static private bool get_asn1_tl(byte[] bsrc, int pos, out int plenlen, out int pdatalen)
        {
            int lenlen, datalen = 0;
            plenlen = pdatalen = 0;
            Byte len1 = bsrc[pos + 1];
            if (len1 < 0x80) lenlen = 1;
            else if (len1 == 0x81) lenlen = 2;
            else if (len1 == 0x82) lenlen = 3;
            else return false;
            if (pos + 1 + lenlen > bsrc.Length) return false;
            if (lenlen == 1) datalen = (int)len1;
            else if (lenlen == 2) datalen = (int)bsrc[pos + 2];
            else if (lenlen == 3) datalen = ((int)bsrc[pos + 2] << 8) + (int)bsrc[pos + 3];
            if (pos + 1 + lenlen + datalen > bsrc.Length) return false;
            plenlen = lenlen;
            pdatalen = datalen;
            return true;
        }
        //----------------------------------------------------------------
        // UTF-8等文字列データ取り出し
        //----------------------------------------------------------------
        static string get_strval(byte[] bsrc, int stpos, int len)
        {
            int lenlen, datalen;
            if (!get_asn1_tl(bsrc, stpos, out lenlen, out datalen)) return null;
            if (1 + lenlen + datalen != len) return null;
            int pos = stpos + 1 + lenlen;
            return System.Text.UTF8Encoding.UTF8.GetString(bsrc, pos, datalen);
        }
        //----------------------------------------------------------------
        // ASN.1内部分解：特定要素取り出し
        //----------------------------------------------------------------
        static int[] get_asn1_inner_idx(byte[] bsrc, int stpos, int len,
                int inneridx, int koteicnt, Byte tag)
        {
            int[] innerpos;
            if (!get_asn1_inner(bsrc, stpos, len, out innerpos)) return null;
            if (inneridx < 0) inneridx = innerpos.Length - 1; // 最終要素
            else if (inneridx >= innerpos.Length) return null;
            if (koteicnt > 0 && koteicnt != innerpos.Length) return null;
            int pos1 = innerpos[inneridx];
            int pos2 = (inneridx == innerpos.Length - 1 ? stpos + len : innerpos[inneridx + 1]);
            if (tag != 0 && bsrc[pos1] != tag) return null;
            int outpos = pos1;
            int outlen = pos2 - pos1;
            return new int[] { outpos, outlen };
        }
        //----------------------------------------------------------------
        // CMSトリム（不要領域削除）
        //----------------------------------------------------------------
        static byte[] trim_cms(byte[] bsrc)
        {
            int lenlen, datalen;
            if (!get_asn1_tl(bsrc, 0, out lenlen, out datalen)) return bsrc;
            int reallen = 1 + lenlen + datalen;
            int srclen = bsrc.Length;
            if (srclen <= reallen) return bsrc;
            byte[] ret = new byte[reallen];
            Array.Copy(bsrc, 0, ret, 0, reallen);
            return ret;
        }
        //----------------------------------------------------------------------
        // 時刻整形
        //----------------------------------------------------------------------
        static private string fmt_asn1time(string str)
        {
            if (str == null) return "";
            DateTime dt = asnstr2date(str);
            if (dt == DateTime.MinValue) return str;
            string ret = string.Format("{0:D4}/{1:D2}/{2:D2} {3:D2}:{4:D2}:{5:D2}",
                     dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            str = str.Replace("Z", "");
            if (str.Length > 14) ret += str.Substring(14); // msec
            return ret;
        }
        //----------------------------------------------------------------------
        // ASN.1日付文字列をDateTime化 （エラーならDateTime.MinValue）
        //----------------------------------------------------------------------
        static private DateTime asnstr2date(string asnstr)
        {
            if (asnstr.Length < 13) return DateTime.MinValue;
            if (asnstr.Length < 15) asnstr = "20" + asnstr;
            string str = asnstr.Substring(0, 4) + "/" +
                asnstr.Substring(4, 2) + "/" + asnstr.Substring(6, 2) + " " +
                asnstr.Substring(8, 2) + ":" + asnstr.Substring(10, 2) + ":" +
                asnstr.Substring(12, 2);
            DateTime dt1;
            if (!DateTime.TryParse(str, out dt1)) return DateTime.MinValue;
            return dt1.ToLocalTime();
        }
    }
}
