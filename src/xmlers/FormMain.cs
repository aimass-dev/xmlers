using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Security.Cryptography.X509Certificates;

namespace xmlers
{
    //----------------------------------------------------------
    // 画面
    //----------------------------------------------------------
    partial class FormMain : Form
    {
        //----------------------------------------------------
        // メンバ変数
        //----------------------------------------------------
        private Tstinfo[] m_listtst = null; // TST
        //----------------------------------------------------
        // コンストラクタ
        //----------------------------------------------------
        internal FormMain()
        {
            InitializeComponent();
        }
        //----------------------------------------------------
        // フォームロードイベント処理
        //----------------------------------------------------
        private void FormMain_Load(object sender, EventArgs e)
        {
            btnCert.Enabled = false;
            init_lvfiles();  // ファイルリストカラム設定
        }
        //----------------------------------------------------
        // ファイルリストカラム設定
        //----------------------------------------------------
        private void init_lvfiles()
        {
            string[] c_colnames = { "No.", "ファイル名","検証結果", "タイムスタンプ", "TSA" };
            int[] c_width = { 40, 200, 140, 150, 180 };
            init_lvcols(lvFiles, c_colnames, c_width);
            lvFiles.OwnerDraw = true;
            lvFiles.DrawItem += (sender, e) => { e.DrawDefault = true; };
            lvFiles.DrawSubItem += (sender, e) => { e.DrawDefault = true; };
            lvFiles.DrawColumnHeader += (sender, e) => {
                e.DrawBackground();
                Rectangle r = e.Bounds;
                Rectangle r2 = new Rectangle(r.X + 2, r.Y + 4, r.Width, r.Height);
                e.Graphics.DrawString(e.Header.Text, e.Font, new SolidBrush(Color.Black), r2);
                Pen pen = new Pen(Color.LightGray);
                e.Graphics.DrawLine(pen, r.Left, r.Bottom-1, r.Right, r.Bottom-1);
            };
        }
        //----------------------------------------------------
        // リストカラム設定
        //----------------------------------------------------
        private void init_lvcols(ListView lv, string[] names, int[] width)
        {
            lv.Columns.Clear();
            int cnt = names.Length;
            for (int i = 0; i < cnt; i++)
            {
                ColumnHeader ch1 = new ColumnHeader();
                ch1.Text = names[i];
                ch1.Width = width[i];
                lv.Columns.Add(ch1);
            }
        }
        //----------------------------------------------------
        // フォルダドラッグエンタ―
        //----------------------------------------------------
        private void lvFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (get_dragged_dir(e.Data) == null)
                e.Effect = DragDropEffects.None;
            else
                e.Effect = DragDropEffects.Move;
        }
        //----------------------------------------------------
        // フォルダドロップ
        //----------------------------------------------------
        private void lvFiles_DragDrop(object sender, DragEventArgs e)
        {
            string dir = get_dragged_dir(e.Data);
            if (dir == null) return;
            disp_indir(dir); // フォルダ選択表示
        }
        //----------------------------------------------------
        // ドラッグされているフォルダ取得
        //----------------------------------------------------
        private string get_dragged_dir(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop)) return null;
            string[] files = (string[])data.GetData(DataFormats.FileDrop, false);
            if (files == null || files.Length != 1) return null;
            string file1 = files[0];
            if (!Directory.Exists(file1)) return null;
            return file1;
        }
        //----------------------------------------------------
        // フォルダ選択表示
        //----------------------------------------------------
        private void disp_indir(string dir)
        {
            Ers ers = new Ers();
            Dirinfo dirinfo = ers.get_dirinfo(dir);
            if(dirinfo == null)
            {
                MessageBox.Show(ers.get_lasterr(), "エラー",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            btnCert.Enabled = false;
            lvFiles.Items.Clear();
            m_listtst = dirinfo.tst;
            string[] tstinfos = current_tstinfo();  // 現在のm_listtstの情報           
            int no = 1;
            Dictionary<int, int> dictst = new Dictionary<int, int>();
            foreach (Docinfo doc1 in dirinfo.doc)
            {
                ListViewItem lvitem1 = new ListViewItem();
                lvitem1.Tag = null;
                string stsstr, tmstr, tsastr;
                stsstr = tmstr = tsastr = " --";
                Color col = Color.Black;
               if(doc1.status == STATUS.VALID)
               {
                    stsstr = "正常";
                   int tstidx = doc1.tstidx;
                   Tstinfo tst = dirinfo.tst[tstidx];
                   if(dictst.ContainsKey(tstidx))
                   {
                       tmstr = "No." + dictst[tstidx] + " と共通";
                       tsastr = "";
                   }
                   else
                   {
                      tmstr = tst.timestr;
                      tsastr = tstinfos[tstidx];
                      dictst[tstidx] = no;
                   }
                   lvitem1.Tag = tstidx;
                   col = Color.Green;
               }
                else if(doc1.status == STATUS.INVALID)
               {
                   stsstr = doc1.errstr;
                   col = Color.Red;
               }
                string[] vals = {"" + no, doc1.fname, stsstr, tmstr, tsastr };
                set_list_line(lvitem1, vals);
                lvFiles.Items.Add(lvitem1);
                lvitem1.ForeColor = col;
                no++;
            }
            lvFiles.GridLines = true;
            lblDir.Text = dir;
            lblMsg.Visible = false;
        }
        //----------------------------------------------------
        // リスト1行項目設定
        //----------------------------------------------------
        private void set_list_line(ListViewItem lvitem, string[] vals)
        {
            int subcnt = vals.Length - 1;
            lvitem.SubItems.Clear();
            if (vals[0] != null) lvitem.Text = vals[0];
            string[] subs = new string[subcnt];
            for (int i = 0; i < subcnt; i++)
            {
                string val1 = vals[i + 1];
                subs[i] = (val1 == null ? "" : val1);
            }
            lvitem.SubItems.AddRange(subs);
        }
     
        //----------------------------------------------------------
        // エラーメッセージ
        //----------------------------------------------------------
        private void do_errmsg(string msg)
        {
            MessageBox.Show(msg, "エラー",
                  MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        //----------------------------------------------------------
        // リスト選択変更
        //----------------------------------------------------------
        private void lvFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            enb_btncert();
        }
        //----------------------------------------------------
        // リストダブルクリック：ファイルオープン
        //----------------------------------------------------
        private void lvFiles_MouseDoubleClick(object sender, MouseEventArgs e)
        {
           if (lvFiles.SelectedItems == null || lvFiles.SelectedItems.Count == 0)
                return;
            ListViewItem lvitem = lvFiles.SelectedItems[0];
            string fpath = Path.Combine(lblDir.Text, lvitem.SubItems[1].Text);
            //string fpath2 = fpath + ".xml";
            //if (File.Exists(fpath2)) fpath = fpath2;
            System.Diagnostics.Process.Start(fpath);
        }
        //----------------------------------------------------------
        // 証明書ボタン制御
        //----------------------------------------------------------
        private void enb_btncert()
        {
            btnCert.Enabled = false;
            if (lvFiles.SelectedItems == null || lvFiles.SelectedItems.Count == 0)
                return;
            ListViewItem lvitem = lvFiles.SelectedItems[0];
            if (lvitem.Tag == null) return;
            btnCert.Enabled = true;
        }
        //----------------------------------------------------------
        // 現在のm_listtstの情報
        //----------------------------------------------------------
        private string[] current_tstinfo()
        {
            List<string> list = new List<string>();
            foreach (Tstinfo tst1 in m_listtst)
            {
                string str;
                if (tst1.certs == null || tst1.certs.Length == 0)
                {
                    str = "TSA証明書なし";
                }
                else
                {
                    X509Certificate2 x509 = new X509Certificate2(tst1.certs[0]);
                    str = x509.GetNameInfo(X509NameType.SimpleName, false);
                }
                list.Add(str);
            }
            return list.ToArray();
        }
        //----------------------------------------------------------
        // TSA証明書表示
        //----------------------------------------------------------
        private void btnCert_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedItems == null || lvFiles.SelectedItems.Count == 0)
                return;
            ListViewItem lvitem = lvFiles.SelectedItems[0];
            if (lvitem.Tag == null) return;
            Tstinfo tst1 = m_listtst[(int)lvitem.Tag];
            if (tst1.certs == null || tst1.certs.Length == 0)
            {
                MessageBox.Show("TSA証明書がありません", "TSA証明書",
                           MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Winapi.showcert(this.Handle, "TSA証明書", tst1.certs);
        }
    }
}
