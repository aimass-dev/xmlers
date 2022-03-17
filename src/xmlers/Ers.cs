using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.Xml;

namespace xmlers
{
    //----------------------------------------------------------
    // XMLERS
    //----------------------------------------------------------
    class Ers
    {
        //----------------------------------------------------
        // メンバ変数
        //----------------------------------------------------
        private string m_lasterr;
        private Random m_rnd = new Random();
        //----------------------------------------------------
        // ディレクトリ情報返却
        //----------------------------------------------------
        internal Dirinfo get_dirinfo(string dir)
        {
            string[] files = Directory.GetFiles(dir);
            if (files.Length == 0)
            {
                m_lasterr = "ファイルが含まれていません。";
                return null;
            }
            Array.Sort<string>(files);
            List<Docinfo> listdoc = new List<Docinfo>();
            List<Tstinfo> listtst = new List<Tstinfo>();
            List<byte[]> listtst_b = new List<byte[]>(); // listtstと同じ並び
            foreach (string file1 in files)
            {
                if (file1.ToLower().EndsWith(".xml"))
                {
                    if (File.Exists(file1.Substring(0, file1.Length - 4))) continue;
                }
                Docinfo doc = get_docinfo(dir, file1, listtst, listtst_b); // ファイル情報1件取得
                listdoc.Add(doc);
            }
            Dirinfo ret = new Dirinfo();
            ret.doc = listdoc.ToArray();
            ret.tst = listtst.ToArray();
            return ret;
        }
        //----------------------------------------------------
        // ファイル情報1件取得
        //-----------------------------------------------------
        private Docinfo get_docinfo(string dir, string fname, List<Tstinfo> listtst, List<byte[]> listtst_b)
        {
            Docinfo doc = new Docinfo();
            doc.fname = Path.GetFileName(fname);
            doc.tstidx = -1;
            string tstfpath = fname + ".xml";
            if (!File.Exists(tstfpath))
            {
                doc.status = STATUS.NONE;
                return doc;
            }
            Ersinfo ers = check_ers(tstfpath, fname, out doc.errstr); // ERSチェック
            if (ers == null) // エラー
            {
                doc.status = STATUS.INVALID;
                return doc;
            }
            byte[] btst = ers.btst;
            for (int i = 0; i < listtst_b.Count; i++)
            {
                if (!is_samebytes(btst, listtst_b[i])) continue;
                doc.tstidx = i;
                break;
            }
            Tstinfo tst = null;
            if (doc.tstidx >= 0)// TST情報既存
            {
                tst = listtst[doc.tstidx];
            }
            else
            {
                tst = get_tstinfo(btst, out doc.errstr); // TST情報取得
                if (tst == null)
                {
                    doc.status = STATUS.INVALID;
                    return doc;
                }
                doc.tstidx = listtst.Count;
                listtst.Add(tst);
                listtst_b.Add(btst);
            }
            if (!is_samebytes(ers.roothash, tst.bhash))
            {
                doc.status = STATUS.INVALID;
                doc.errstr = "ルートハッシュ不一致";
                return doc;
            }
            doc.status = STATUS.VALID; // OK
            return doc;
        }
        //----------------------------------------------------
        // 最終エラー返却
        //-----------------------------------------------------
        internal string get_lasterr()
        {
            return m_lasterr;
        }
        //----------------------------------------------------
        // ERSチェック
        //-----------------------------------------------------
        private Ersinfo check_ers(string tstfpath, string docfpath, out string errstr)
        {
            errstr = null;
            ErsinfoR ers2 = Xml.read_ers(tstfpath, out errstr); // ERSファイル読み込み
            if (ers2 == null) return null;
            HashAlgorithm hashalgo = get_hashalgo(ers2.hashidx);
            byte[] dochash = get_filehash(hashalgo, docfpath, out errstr);　// 原本ハッシュ
            if (dochash == null) return null;
            Ersinfo ers = new Ersinfo();
            ers.hashidx = ers2.hashidx;
            ers.btst = ers2.btst;
            if (ers2.hashgrp == null)
            {
                ers.roothash = dochash;　// 文書ハッシュがルートハッシュ
                return ers;
            }
            bool hash_found = false;
            foreach (byte[] bhash1 in ers2.hashgrp[0].bhash)
            {
                if (hash_found = is_samebytes(bhash1, dochash)) break;
            }
            if (!hash_found)
            {
                errstr = "文書ハッシュ不一致";
                return null;
            }
            byte[] bhash_next = null;
            foreach (Hashgrp hgrp1 in ers2.hashgrp)
            {
                List<byte[]> list = new List<byte[]>(hgrp1.bhash);
                if (bhash_next != null) list.Add(bhash_next);
                if (list.Count == 1)
                {
                    bhash_next = list[0];
                    continue;
                }
                list.Sort(Barrcmp.getinst()); // ソート
                byte[] barr = join_barr(list.ToArray()); // ハッシュ結合
                bhash_next = hashalgo.ComputeHash(barr);
            }
            ers.roothash = bhash_next; // ルートハッシュ
            return ers;
        }
        //----------------------------------------------------
        // RFC3161タイムスタンプ情報取得
        //-----------------------------------------------------
        private Tstinfo get_tstinfo(byte[] btst, out string errstr)
        {
            errstr = null;
            Tstinfo info = Asn1.get_tstinfo(btst, out errstr); // タイムスタンプ情報取得
            if (info == null) return null;
            SignedCms cms = new SignedCms();
            try
            {
                cms.Decode(btst);        // 検証準備読込み
                cms.CheckSignature(true);// タイムスタンプ署名検証
            }
            catch(Exception e)
            {
                errstr = e.Message;
                return null;
            }
            return info;
        }
        //-----------------------------------------------------------------
        // ハッシュアルゴリズムインスタンス返却
        //-----------------------------------------------------------------
        internal static HashAlgorithm get_hashalgo(int hashidx)
        {
            if (hashidx == 1) return new SHA256Managed();
            if (hashidx == 2) return new SHA384Managed();
            if (hashidx == 3) return new SHA512Managed();
            return new SHA1Managed();
        }
        //-----------------------------------------------------------------
        // ファイルハッシュ計算
        //-----------------------------------------------------------------
        private byte[] get_filehash(HashAlgorithm hashalgo, string fname, out string errstr)
        {
            errstr = null;
            FileStream fs = null;
            Stream stm = null;
            try
            {
                fs = new FileStream(fname, FileMode.Open, FileAccess.Read);
                if (fname.ToLower().EndsWith(".xml"))  // 正規化あり
                {
                    XmlDsigC14NTransform trans = new XmlDsigC14NTransform(false); // not include comment
                    trans.LoadInput(fs);
                    stm = (Stream)trans.GetOutput(typeof(Stream));
                    return hashalgo.ComputeHash(stm); // ハッシュ計算
                }
                else   // 正規化なし
                {
                    return hashalgo.ComputeHash(fs); // ハッシュ計算
                }
            }
            catch (Exception ex)
            {
                errstr = ex.ToString();
                return null;
            }
            finally
            {
                if (fs != null) fs.Close();
                if (stm != null) stm.Close();
            }
        }
        //-----------------------------------------------------------------
        // バイト配列比較
        //-----------------------------------------------------------------
        class Barrcmp : IComparer<byte[]>
        {
            private static Barrcmp m_inst = new Barrcmp();
            internal static Barrcmp getinst()
            {
                return m_inst;
            }
            public int Compare(byte[] a1, byte[] a2)
            {
                int len = a1.Length;
                if (len > a2.Length) len = a2.Length;
                for (int i = 0; i < len; i++)
                {
                    int chk = (int)a1[i] - (int)a2[i];
                    if (chk != 0) return chk;
                }
                return a1.Length - a2.Length;
            }
        }
        //-----------------------------------------------------------------
        // バイト配列結合
        //-----------------------------------------------------------------
        private byte[] join_barr(byte[][] barr)
        {
            int len = 0;
            foreach (byte[] b1 in barr)
                len += b1.Length;
            byte[] bdst = new byte[len];
            int off = 0;
            foreach (byte[] b1 in barr)
            {
                Array.Copy(b1, 0, bdst, off, b1.Length);
                off += b1.Length;
            }
            return bdst;
        }
        //-----------------------------------------------------------------
        // バイト配列比較（bool）
        //-----------------------------------------------------------------
        private bool is_samebytes(byte[] a1, byte[] a2)
        {
            int len = a1.Length;
            if (len != a2.Length) return false;
            for (int i = 0; i < len; i++)
            {
                if (a1[i] != a2[i]) return false;
            }
            return true;
        }
    }
    //----------------------------------------------------------
    // ディレクトリ情報
    //----------------------------------------------------------
    class Dirinfo
    {
        internal Docinfo[] doc; // 原本
        internal Tstinfo[] tst; // TST
    }
    //----------------------------------------------------------
    // 原本情報
    //----------------------------------------------------------
    class Docinfo
    {
        internal string fname;  // ファイル名
        internal STATUS status; // 検証状態
        internal int tstidx;    // TSTインデクス
        internal string errstr; // エラー文字列
    }
    //----------------------------------------------------------
    // 検証状態　TSTなし,有効,無効
    //----------------------------------------------------------
    enum STATUS
    {
        NONE, VALID, INVALID
    }
    //----------------------------------------------------------
    // TST情報
    //----------------------------------------------------------
    class Tstinfo
    {
        internal byte[] bhash;  // 文書ハッシュ
        internal string timestr; // 時刻文字列
        internal byte[][] certs; // 証明書
    }
    //----------------------------------------------------------
    // ERS情報
    //----------------------------------------------------------
    class Ersinfo
    {
        internal int hashidx; // 0:sha1 1:sha256 2:sha384 3:sha512
        internal byte[] roothash; // ルートハッシュ
        internal byte[] btst; // TST
    }
    //----------------------------------------------------------
    // ERS情報（XML読み込み用）
    //----------------------------------------------------------
    class ErsinfoR
    {
        internal int hashidx; // 0:sha1 1:sha256 2:sha384 3:sha512
        internal Hashgrp[] hashgrp; // ハッシュグループ
        internal byte[] btst; // TST
    }
    //----------------------------------------------------------
    // ハッシュグループ(order[] + roothash)
    //----------------------------------------------------------
    internal class Hashgrp
    {
        internal byte[][] bhash;
        internal Hashgrp(byte[][] i_bhash) { bhash = i_bhash; }
    }
}

