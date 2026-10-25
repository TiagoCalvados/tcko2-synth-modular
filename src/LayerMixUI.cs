using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
namespace TCKO2Synth {
partial class MainForm {
    LayeredSynth Rack{get{return (LayeredSynth)Engine;}}
    bool loadingLayers=true,syncingLayers;int layerVersion=-1;
    readonly ComboBox layerList=new Choice();readonly Button removeLayer=new Button();
    readonly CheckBox mixGlitch=new CheckBox(),mixElectro=new CheckBox();
    void InitializeLayerMix(TableLayoutPanel root){
        var holds=(Panel)root.GetControlFromPosition(0,3);
        glitchToggle.Visible=glitchControl.Visible=glitchTypeChoice.Visible=false;
        fragmentsToggle.SetBounds(350,2,110,26);fragmentsControl.SetBounds(460,0,100,32);
        holds.Controls.Add(LabelAt("LAYERS",566,7,65,22,8,Theme.Mint));StyleCombo(layerList);layerList.Dock=DockStyle.None;layerList.SetBounds(631,0,203,30);layerList.DropDownWidth=420;layerList.AccessibleName="Sounds kept in the keyboard mix";holds.Controls.Add(layerList);
        removeLayer.Text="Remove";StyleButton(removeLayer,Theme.Card);removeLayer.SetBounds(842,0,100,30);removeLayer.AccessibleName="Remove selected sound layer";holds.Controls.Add(removeLayer);
        layerList.SelectedIndexChanged+=delegate{if(!syncingLayers&&layerList.SelectedItem is SoundLayer)Rack.SelectLayer(((SoundLayer)layerList.SelectedItem).Id);};
        removeLayer.Click+=delegate{var selected=layerList.SelectedItem as SoundLayer;if(selected!=null){Rack.RemoveLayer(selected.Id);RefreshLayers();}};
        loadingLayers=false;timer.Tick+=delegate{RefreshLayers();};RefreshLayers();
    }
    void RefreshLayers(){
        // Native dropdowns keep their highlighted item separately until the click commits.
        // Reapplying the committed index cancels that pending mouse selection.
        if(layerList.DroppedDown||boxGlitchChoice.DroppedDown||technologyChoice.DroppedDown||electroChoice.DroppedDown)return;
        syncingLayers=true;
        try{
            if(layerVersion!=Rack.Version){var current=Rack.Current;layerList.BeginUpdate();layerList.Items.Clear();layerList.Items.AddRange(Rack.Layers);if(current!=null)layerList.SelectedItem=current;layerList.EndUpdate();layerVersion=Rack.Version;removeLayer.Enabled=layerList.Items.Count>0;effectHints.SetToolTip(layerList,Rack.Layers.Length+" / "+LayeredSynth.LayerLimit+" layers · "+String.Join(" + ",Rack.Layers.Select(x=>x.Name)));}
            mixGlitch.Checked=Engine.GlitchMode;SyncLayerChoice(boxGlitchChoice,(int)Engine.SelectedGlitchType);boxGlitchLevel.Value=(int)Math.Round(Engine.GlitchAmount*100);mixElectro.Checked=Rack.ElectroLayer!=null&&Rack.ElectroLayer.On;
            technologyKeyboard.Checked=Rack.TechnologyLayer!=null&&Rack.TechnologyLayer.On;
            if(Rack.TechnologyLayer!=null){SyncLayerChoice(technologyChoice,Rack.TechnologyLayer.Index);technologyCharacter.Value=(int)Math.Round(Rack.TechnologyLayer.Character*100);technologyLevel.Value=(int)Math.Round(Rack.TechnologyLayer.Level*100);}
            if(Rack.ElectroLayer!=null){SyncLayerChoice(electroChoice,Rack.ElectroLayer.Index-11);electroLevel.Value=(int)Math.Round(Rack.ElectroLayer.Level*100);}
            activity.Text=Rack.Message;
        }finally{syncingLayers=false;}
    }
    static void SyncLayerChoice(ComboBox choice,int selected){if(!choice.DroppedDown&&choice.SelectedIndex!=selected)choice.SelectedIndex=selected;}
    Control SoundSection(bool glitch){
        var choice=glitch?boxGlitchChoice:electroChoice;var toggle=glitch?mixGlitch:mixElectro;var level=glitch?boxGlitchLevel:electroLevel;
        var box=new GroupBox{Text=glitch?"GLITCH · MIX EFFECT":"OLD ELECTRO · ADD LAYERS",Dock=DockStyle.Fill,ForeColor=glitch?Theme.Mint:Theme.Copper,Padding=new Padding(8),Margin=new Padding(glitch?0:6,0,glitch?6:0,0)};
        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=Padding.Empty};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int height in new[]{29,27,36,18,27})grid.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        StyleCombo(choice);choice.Margin=Padding.Empty;choice.AccessibleName=glitch?"Glitch effect for the keyboard mix":"Old Electro sound to add";if(glitch)choice.Items.AddRange(glitchTypes);else for(int i=11;i<Patch.All.Length;i++)choice.Items.Add(Patch.All[i].Name.Substring(4));choice.SelectedIndex=glitch?0:1;grid.Controls.Add(choice,0,0);
        Action choose=()=>{if(loadingLayers||syncingLayers)return;if(glitch){Engine.SetGlitchType((GlitchType)choice.SelectedIndex);glitchTypeChoice.SelectedIndex=choice.SelectedIndex;Engine.SetGlitch(true);UpdateHold();}else Rack.AddElectro(choice.SelectedIndex+11);RefreshLayers();};
        choice.SelectedIndexChanged+=delegate{choose();};choice.SelectionChangeCommitted+=delegate{choose();};
        toggle.Text=glitch?"Glitch on the mix":"Selected layer on";toggle.FlatStyle=FlatStyle.Flat;toggle.ForeColor=glitch?Theme.Mint:Theme.Copper;toggle.Dock=DockStyle.Fill;toggle.Margin=Padding.Empty;toggle.AccessibleName=toggle.Text;toggle.CheckedChanged+=delegate{if(syncingLayers)return;if(glitch){Engine.SetGlitch(toggle.Checked);UpdateHold();}else{if(Rack.ElectroLayer==null&&toggle.Checked)Rack.AddElectro(choice.SelectedIndex+11);Rack.SetLayerOn(Rack.ElectroLayer,toggle.Checked);}RefreshLayers();};grid.Controls.Add(toggle,0,1);
        var hint=new Label{Text=glitch?"Shapes every sound in the mix. No separate note player.":"Choose a sound to add it. Previous sounds keep playing on the same keys.",Dock=DockStyle.Fill,ForeColor=Theme.Dim,Font=Theme.Font(8,FontStyle.Regular),Margin=Padding.Empty};grid.Controls.Add(hint,0,2);
        grid.Controls.Add(new Label{Text=glitch?"STRENGTH":"SELECTED LAYER VOLUME",Dock=DockStyle.Fill,ForeColor=Theme.Dim,Font=Theme.Font(8,FontStyle.Regular),Margin=Padding.Empty},0,3);
        level.Minimum=0;level.Maximum=100;level.Value=glitch?65:100;level.TickStyle=TickStyle.None;level.Dock=DockStyle.Fill;level.Margin=Padding.Empty;level.AccessibleName=glitch?"Glitch mix strength":"Selected Old Electro layer volume";level.ValueChanged+=delegate{if(syncingLayers)return;if(glitch){Engine.SetGlitchAmount(level.Value/100.0);glitchControl.Value=level.Value;}else Rack.SetLayerLevel(Rack.ElectroLayer,level.Value/100.0);};grid.Controls.Add(level,0,4);box.Controls.Add(grid);return box;
    }
}
}

