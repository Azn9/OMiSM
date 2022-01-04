using System;
using System.Linq;
using System.Windows.Forms;

namespace OMtoSMConverter
{
    public partial class Form2 : Form
    {
        public new Form1 Parent;
        private bool _ready;
        public Form2()
        {
            InitializeComponent();
            _ready = false;
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            hsFrom.Items.Clear();
            hsTo.Items.Clear();
            hsFrom.Items.Add("Drag and drop a single osu file containing the keysound data here!");
            hsFrom.Items.Add("Alternatively, drop all the osu files involved, and only one difficulty named 'Key*' will be used as the keysound data.");
            hsFrom.Items.Add("Press 'c' to return to conversion.");
            hsTo.Items.Add("Drag and drop the destination osu files here!");
        }

        //File read and access
        private void FileDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }
        private void HsDragDrop(object sender, DragEventArgs e)
        {
            // Copied from fileBeg_DragDrop
            //Activate the window to focus it on front
            this.Activate();

            //Get the filenames of the dropped stuff
            var data = e.Data.GetData(DataFormats.FileDrop);
            var allDroppedFiles = (string[])data;

            //Search all directories for files and remove all directories
            var allFoundFiles = Parent.FoundFiles(allDroppedFiles);
            if (sender.Equals(hsFrom))
            {
                hsFrom.Items.Clear();
                hsTo.Items.Clear();
                if (allFoundFiles.Count == 1)
                {
                    hsFrom.Items.Add(allFoundFiles.First());
                }
                else
                {
                    foreach (var file in allFoundFiles)
                    {
                        if (file.Contains("[Key") && hsFrom.Items.Count < 1) hsFrom.Items.Add(file);
                        else
                        {
                            if (file.Substring(file.Length-4) == ".osu")
                                hsTo.Items.Add(file);
                        }
                    }
                }
            }
            else if (sender.Equals(hsTo))
            {
                hsTo.Items.Clear();
                foreach (var file in allFoundFiles) hsTo.Items.Add(file);
            }
            if ((hsFrom.Items.Count == 1) && (hsTo.Items.Count >= 1)) _ready = true;
            doButton.Text = @"Do";
        }

        //Usability
        private void HsKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != 'c')
                return;
            
            Parent.Show();
            this.Close();
        }
        private void HsClose(object sender, FormClosedEventArgs e)
        {
            Parent.Close();
        }

        //Actually doing stuff
        private void DoHsCopy(object sender, EventArgs e)
        {
            if (_ready)
            {
                var source = Beatmap.GetRawOsuFile((string)hsFrom.Items[0]);
                foreach (string dest in hsTo.Items)
                {
                    var lastSlash = dest.LastIndexOf("\\", StringComparison.Ordinal);
                    var folder = dest.Substring(0, lastSlash);

                    var toMap = Beatmap.GetRawOsuFile(dest);
                    toMap.Backup(folder);
                    toMap.CopyHs(source);
                    toMap.WriteOut(folder);
                    
                }
            }
            else
            {
                doButton.Text = @"You haven't shown me all the files yet!";
            }
        }


    }
}
