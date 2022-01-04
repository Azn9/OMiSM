using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

/* A quick description of the algorithm
 * 
 * OM -> SM:
 * Timing points will be treated as the beginning of the measure always. 
 * The BPM will change a lot more than needed, but we can make up for this by using a higher quantization.
 * Notes will be placed according to their location in the BPM space.
 */

/* TODO
 * Bug with #fairy dancing in lake
 * Standardize header data
 * Proper notification of inability to copy files
 * Copy mp3s asynchronously (createSingleSMFile())
 * Figure out what's causing the lag
 * Completely fails when there are more than five diffs of the same keycount in the same file
 * 
 * 
 * DONE
 * Multiple directory support
 * Multiple mp3s/files at a time support.
 * Standardize the sm Start time across all difficulties.
 */

namespace OMtoSMConverter
{
    public partial class Form1 : Form
    {
        //Initializations
        public Form1()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            InitializeComponent();
            BeginInstructions();
        }

        //Stuff that happens
        private void ProcessOsuFiles(IEnumerable<string> filepaths)
        {
            //
            //<summary>
            //This program begins the process of converting the files after dropping various files of different kinds into the program.
            //</summary>

            //Initialize a set of all available mp3 files and to hold files after initial parsint
            var allParses = new Dictionary<string, HashSet<Beatmap>>();
            var count = 0;

            //Parse all dropped files that are osu files
            foreach (var filepath in filepaths)
            {
                if (filepath.Split('.').Last() != "osu")
                    continue;

                var newParse = Beatmap.GetRawOsuFile(filepath);
                count++;

                //Then add it to the dictionary for later use
                if (allParses.ContainsKey(newParse.AudioPath)) allParses[newParse.AudioPath].Add(newParse);
                else
                {
                    var newSet = new HashSet<Beatmap> {newParse};
                    allParses.Add(newParse.AudioPath, newSet);
                }
            }

            //Inform user of progress
            BoxInform($"{count} total osu files found.",
                $"{allParses.Keys.Count()} potential Stepmania files may be created.");


            //Then we can treat each mp3 as its own smFile
            foreach (var audioFile in allParses.Keys)
            {
                BoxInform("Now processing:", audioFile);
                CreateSingleSmFile(allParses[audioFile]);
            }

            BoxInform("All conversions complete!");
        }

        private void CreateSingleSmFile(IEnumerable<Beatmap> diffs)
        {
            //Create a new smFile ready to use
            var file = new SmFile();

            //First, add raw diffs to smFile
            foreach (var diff in diffs)
            {
                //Goes ahead and standardizes the starting point of all difficulties.
                file.Add(diff);
            }

            //Figure out where to put the diffs. Also kills all non-mania maps.
            file.AutoSortDiffs();

            //Checks and breaks if there is no files
            if (file.AllDiffs.Count == 0)
            {
                BoxInform("No osu!mania beatmaps found!");
                return;
            }

            //Inform user of progress
            var progress = file.AllDiffs.Count() + " osu!mania beatmaps determined.";
            BoxInform(progress);

            //Convert all diffs
            foreach (var diff in file.AllDiffs)
            {
                BoxInform("Converting: " + diff.OMetadata["Version"]);
                if (diff.SmKeyType() != "")
                {
                    file.HasSubstamce = true;

                    var converterThread = new Thread(() => diff.FillSMfromOsu());
                    converterThread.Start();

                    while (converterThread.IsAlive)
                    {
                    }
                }
                else
                {
                    BoxInform("Unsupported key count, beatmap not converted.");
                }
            }

            //Write to file
            BoxInform("Exporting the Stepmania files, copying the mp3s might take a while...");

            file.SetHeaderData();
            file.WriteEverythingToFolder(this);

            //Inform of Completion
            BoxInform("File complete!");
        }

        private void fileBeg_dragDrop(object sender, DragEventArgs e)
        {
            //Activate the window to focus it on front
            Activate();

            //Get the filenames of the dropped stuff
            var data = e.Data.GetData(DataFormats.FileDrop);
            var allDroppedFiles = (string[]) data;

            BoxInform(allDroppedFiles.Count() + " files dropped.");

            //Search all directories for files and remove all directories
            var allFoundFiles = FoundFiles(allDroppedFiles);

            //Begin processing the files
            ProcessOsuFiles(allFoundFiles);
        }

        private void fileBeg_dragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void WinKeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'e':
                    BoxClear();
                    break;
                case 'h':
                    HsCopier();
                    break;
            }
        }

        //GUI I/O
        public void BoxInform(string progress, bool verbose = false)
        {
            outBox.Items.Add(progress);
            var visibleItems = outBox.ClientSize.Height / outBox.ItemHeight;
            outBox.TopIndex = Math.Max(outBox.Items.Count - visibleItems + 1, 0);
            outBox.Update();
        }

        private void BoxInform(params string[] progresses)
        {
            foreach (var progress in progresses)
            {
                BoxInform(progress);
            }
        }

        private void BeginInstructions()
        {
            BoxInform("Drag and drop a bunch of osu files and folders here!",
                "Press 'e' to clear this status box.",
                "Press 'h' to copy hitsounds");
        }

        private void BoxClear()
        {
            outBox.Items.Clear();
            BeginInstructions();
        }

        private void HsCopier()
        {
            var hitsoundCopier = new Form2();
            hitsoundCopier.Show();
            hitsoundCopier.Parent = this;
            Hide();
        }

        //File Handling
        public HashSet<string> FoundFiles(IEnumerable<string> allDroppedFiles)
        {
            //Search all directories for files and remove all directories from the set
            var outFiles = new HashSet<string>();
            foreach (var path in allDroppedFiles)
            {
                if (File.Exists(path)) outFiles.Add(path);
                else if (Directory.Exists(path))
                {
                    var filenames = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    outFiles.UnionWith(filenames);
                }
            }

            return outFiles;
        }

        public static string DirnameSanitize(string bad)
        {
            //HashSet<char> invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            return string.Join("_", bad.Split(Path.GetInvalidPathChars()));
        }

        public static string FilenameSanitize(string bad)
        {
            //HashSet<char> invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            return string.Join("_", bad.Split(Path.GetInvalidFileNameChars()));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void outBox_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
    }
}