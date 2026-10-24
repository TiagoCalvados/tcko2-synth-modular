using System;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TCKO2Synth {
static class HardwareChecks {public static bool Skip;}
partial class MainForm {
    readonly Button openSound=new Button(),playSound=new Button(),stopSound=new Button();
    readonly Label soundFileStatus=new Label();bool loadingSound;int soundTransportRevision;
    internal Func<OpenFileDialog,IWin32Window,DialogResult> SoundPicker=(dialog,owner)=>dialog.ShowDialog(owner);
    internal Task PendingSound=Task.FromResult(0);
    void InitializeSoundFiles(TableLayoutPanel root) {
        var bar=(Panel)root.GetControlFromPosition(0,2);description.Width=310;description.AutoEllipsis=true;
        openSound.Text="OPEN SOUND";playSound.Text="PLAY SOUND";stopSound.Text="STOP SOUND";
        Button[] buttons={openSound,playSound,stopSound};for(int i=0;i<buttons.Length;i++){StyleButton(buttons[i],Theme.Card);buttons[i].Dock=DockStyle.None;buttons[i].AccessibleName=buttons[i].Text;bar.Controls.Add(buttons[i]);}
        var heading=(Panel)root.GetControlFromPosition(0,0);heading.Controls.Add(openSound);
        Action headerLayout=()=>{int left=Math.Min(430,Math.Max(350,heading.Width-470));openSound.SetBounds(left,0,124,32);heading.Controls[0].Width=left-10;heading.Controls[1].Width=left+124;foreach(var label in new[] {midiStatus,audioStatus,activity}){label.Left=left+138;label.Width=Math.Max(80,heading.Width-label.Left);}};heading.Resize+=delegate{headerLayout();};headerLayout();
        playSound.SetBounds(655,44,150,26);stopSound.SetBounds(817,44,134,26);playSound.Anchor=stopSound.Anchor=AnchorStyles.Top|AnchorStyles.Right;playSound.Font=stopSound.Font=Theme.Font(8,FontStyle.Bold);
        soundFileStatus.Text="Open your own sound";soundFileStatus.AutoEllipsis=true;soundFileStatus.SetBounds(328,44,305,26);soundFileStatus.TextAlign=ContentAlignment.MiddleLeft;soundFileStatus.ForeColor=Theme.Copper;soundFileStatus.Font=Theme.Font(8,FontStyle.Regular);bar.Controls.Add(soundFileStatus);
        presets.SelectedIndexChanged+=delegate {effectHints.SetToolTip(description,description.Text);};
        openSound.Click+=delegate {ChooseSound();};
        playSound.Click+=delegate {if(Engine.Stopped){soundFileStatus.Text="Press Resume sound before playing the file.";return;}boxes.File.Play();UpdateFileControls();};stopSound.Click+=delegate {StopSoundFile();UpdateFileControls();};
        timer.Tick+=delegate {UpdateFileControls();};UpdateFileControls();
    }
    internal OpenFileDialog SoundDialog() {return new OpenFileDialog {Title="Open sound · TCK02",InitialDirectory=SoundImport.Folder,Filter=SoundImport.Filter,Multiselect=false,RestoreDirectory=true};}
    void ChooseSound() {
        if(loadingSound)return;
        try {Directory.CreateDirectory(SoundImport.Folder);using(var dialog=SoundDialog())if(SoundPicker(dialog,this)==DialogResult.OK)PendingSound=LoadSoundFile(dialog.FileName);}
        catch(Exception error){soundFileStatus.Text="Could not open sound: "+error.Message;}
    }
    internal async Task LoadSoundFile(string path) {
        if(loadingSound)return;loadingSound=true;openSound.Enabled=false;int revision=soundTransportRevision;
        try {
            var decoded=await Task.Run(()=>SoundImport.Read(path));
            if(IsDisposed)return;
            boxes.File.Load(decoded,!Engine.Stopped && revision==soundTransportRevision);soundFileStatus.Text=decoded.Name+" · "+decoded.Seconds.ToString("0.##")+" s";effectHints.SetToolTip(soundFileStatus,Path.GetFullPath(path));
        } catch(Exception error){if(!IsDisposed)soundFileStatus.Text="Sound retained · "+error.Message;}
        finally {loadingSound=false;if(!IsDisposed){openSound.Enabled=true;UpdateFileControls();}}
    }
    void UpdateFileControls() {playSound.Enabled=boxes.File.Clip!=null;stopSound.Enabled=boxes.File.Playing;}
    void StopSoundFile() {soundTransportRevision++;boxes.File.Stop();}
}
}
