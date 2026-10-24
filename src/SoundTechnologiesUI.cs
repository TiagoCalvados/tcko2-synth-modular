using System;
using System.Drawing;
using System.Windows.Forms;

namespace TCKO2Synth {
partial class MainForm {
    readonly ComboBox technologyChoice=new Choice();
    readonly Label technologyKeyboard=new Label();
    readonly TrackBar technologyCharacter=new TrackBar(),technologyLevel=new TrackBar();
    readonly Label technologyDescription=new Label();
    Control InitializeTechnologies(){
        GroupBox group=new GroupBox{Text="SOUND TECHNOLOGIES",Name="SoundTechnologies",Dock=DockStyle.Fill,ForeColor=Theme.Mint,Padding=new Padding(8),Margin=new Padding(5,0,5,0)};
        TableLayoutPanel grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=5,Margin=new Padding(0)};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,55));
        foreach(int height in new int[]{27,29,26,24,24})grid.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        StyleCombo(technologyChoice);technologyChoice.AccessibleName="Sound technology";technologyChoice.Items.AddRange(TechnologyNames.Labels);technologyChoice.Margin=new Padding(0);grid.Controls.Add(technologyChoice,0,0);grid.SetColumnSpan(technologyChoice,3);
        technologyDescription.ForeColor=Theme.Dim;technologyDescription.Font=Theme.Font(7.5f,FontStyle.Regular);technologyDescription.Dock=DockStyle.Fill;technologyDescription.Margin=new Padding(0);grid.Controls.Add(technologyDescription,0,2);grid.SetColumnSpan(technologyDescription,3);
        technologyKeyboard.Text="Select a technology, then play keys";technologyKeyboard.ForeColor=Theme.Mint;technologyKeyboard.TextAlign=ContentAlignment.MiddleLeft;technologyKeyboard.Font=Theme.Font(9,FontStyle.Bold);technologyKeyboard.Dock=DockStyle.Fill;technologyKeyboard.Margin=new Padding(0,2,0,0);technologyKeyboard.AccessibleName="Use selected technology on the keyboard";grid.Controls.Add(technologyKeyboard,0,1);grid.SetColumnSpan(technologyKeyboard,3);
        int row=3;foreach(TrackBar slider in new TrackBar[]{technologyCharacter,technologyLevel}){
            bool character=slider==technologyCharacter;slider.Minimum=0;slider.Maximum=100;slider.Value=character?50:35;slider.TickStyle=TickStyle.None;slider.Dock=DockStyle.Fill;slider.Margin=new Padding(0);slider.AccessibleName=character?"Technology character":"Technology volume";
            Label label=new Label{Text=character?"Character":"Volume",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Dim,Font=Theme.Font(8,FontStyle.Regular),Margin=new Padding(0)};
            grid.Controls.Add(label,0,row);grid.Controls.Add(slider,1,row);grid.SetColumnSpan(slider,2);row++;
        }
        technologyChoice.SelectedIndexChanged+=delegate{UseTechnology();technologyDescription.Text=TechnologyNames.Descriptions[technologyChoice.SelectedIndex];effectHints.SetToolTip(technologyChoice,technologyDescription.Text);};
        technologyCharacter.ValueChanged+=delegate{ApplyTechnology();};technologyLevel.ValueChanged+=delegate{ApplyTechnology();};
        technologyChoice.SelectionChangeCommitted+=delegate{UseTechnology();};
        technologyChoice.SelectedIndex=0;group.Controls.Add(grid);
        timer.Tick+=delegate{bool active=Engine.TechnologyType>=0;technologyKeyboard.Text=active?"KEYBOARD ACTIVE · PLAY THE KEYS":"Select a technology, then play keys";technologyKeyboard.BackColor=active?Color.FromArgb(40,71,59):Theme.Card;};
        return group;
    }
    void ApplyTechnology(){Engine.TechnologyControls(technologyCharacter.Value/100.0,technologyLevel.Value/100.0);}
    void UseTechnology(){if(technologyChoice.SelectedIndex<0)return;bool changed=Engine.TechnologyType!=technologyChoice.SelectedIndex;Engine.SelectTechnology(technologyChoice.SelectedIndex);ApplyTechnology();if(changed)heldKeys.Clear();description.Text="KEYBOARD · "+TechnologyNames.Labels[technologyChoice.SelectedIndex];UpdateHold();activity.Text="Sound Technologies · play the keys. Selecting a sound is silent.";}
}
}
