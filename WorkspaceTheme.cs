using System.Drawing.Drawing2D;
namespace KiwiDX;

internal enum RadioGlyph { None, Wave, Globe, Star, Chat, Speaker, Gear, Share, User, Info, Dot, Close, Minus, Plus }
internal static class WorkspaceTheme
{
    internal static readonly Color Background = Color.FromArgb(14, 24, 33), Surface = Color.FromArgb(21, 34, 46), Input = Color.FromArgb(17, 29, 40), Border = Color.FromArgb(53, 76, 96), Text = Color.FromArgb(228, 237, 246), Muted = Color.FromArgb(155, 175, 193), Green = Color.FromArgb(109, 239, 125), Blue = Color.FromArgb(77, 159, 242);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
    internal static void ApplyWindowStyle(Form form)
    {
        void DarkCaption(){int dark=1,caption=Surface.R|(Surface.G<<8)|(Surface.B<<16),text=Text.R|(Text.G<<8)|(Text.B<<16);try{DwmSetWindowAttribute(form.Handle,20,ref dark,4);DwmSetWindowAttribute(form.Handle,35,ref caption,4);DwmSetWindowAttribute(form.Handle,36,ref text,4);}catch(DllNotFoundException){}}
        form.HandleCreated+=(_,_)=>DarkCaption();if(form.IsHandleCreated)DarkCaption();
    }
    internal static GraphicsPath Round(RectangleF r, float radius = 5)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
    }
    internal static void Glyph(Graphics g, RadioGlyph kind, RectangleF bounds, Color color)
    {
        var state=g.Save(); g.TranslateTransform(bounds.X,bounds.Y); g.ScaleTransform(bounds.Width/24,bounds.Height/24); g.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(color,1.65f) { StartCap=LineCap.Round, EndCap=LineCap.Round, LineJoin=LineJoin.Round }; using var brush=new SolidBrush(color);
        switch(kind)
        {
            case RadioGlyph.Wave: g.DrawLines(pen,new PointF[]{new(1,18),new(4,18),new(6,4),new(8,22),new(11,8),new(13,17),new(15,2),new(17,20),new(19,9),new(21,14),new(23,14)}); break;
            case RadioGlyph.Globe: g.DrawEllipse(pen,3,3,18,18); g.DrawEllipse(pen,8,3,8,18); g.DrawLine(pen,3,12,21,12); g.DrawArc(pen,4,3,16,9,0,180); break;
            case RadioGlyph.Star:
                var pts=Enumerable.Range(0,10).Select(i=>{double a=-Math.PI/2+i*Math.PI/5;float r=i%2==0?10:4.6f;return new PointF(12+(float)Math.Cos(a)*r,12+(float)Math.Sin(a)*r);}).ToArray(); g.FillPolygon(brush,pts); break;
            case RadioGlyph.Chat: g.FillEllipse(brush,2,2,20,16); g.FillPolygon(brush,new PointF[]{new(5,13),new(3,22),new(13,16)}); break;
            case RadioGlyph.Speaker: g.FillPolygon(brush,new PointF[]{new(2,9),new(7,9),new(12,4),new(12,20),new(7,15),new(2,15)});g.DrawArc(pen,10,6,9,12,-65,130);g.DrawArc(pen,10,2,14,20,-65,130);break;
            case RadioGlyph.Gear:
                for(int i=0;i<8;i++){var s=g.Save();g.TranslateTransform(12,12);g.RotateTransform(i*45);g.FillRectangle(brush,-2,-11,4,6);g.Restore(s);}g.FillEllipse(brush,5,5,14,14);using(var hole=new SolidBrush(Surface))g.FillEllipse(hole,9,9,6,6);break;
            case RadioGlyph.Share: g.DrawLines(pen,new PointF[]{new(7,9),new(3,9),new(3,22),new(21,22),new(21,9),new(17,9)});g.DrawLine(pen,12,16,12,2);g.DrawLines(pen,new PointF[]{new(7,7),new(12,2),new(17,7)});break;
            case RadioGlyph.User: g.FillEllipse(brush,8,2,8,8);g.FillPie(brush,4,12,16,16,180,180);g.FillRectangle(brush,4,19,16,3);break;
            case RadioGlyph.Info: using(var path=Round(new RectangleF(2,2,20,20),3))g.DrawPath(pen,path);g.FillEllipse(brush,11,6,2,2);g.DrawLine(pen,12,11,12,18);break;
            case RadioGlyph.Dot: g.FillEllipse(brush,6,6,12,12);break;
            case RadioGlyph.Close:g.DrawLine(pen,6,6,18,18);g.DrawLine(pen,18,6,6,18);break;
            case RadioGlyph.Minus:g.DrawLine(pen,6,12,18,12);break;
            case RadioGlyph.Plus:g.DrawLine(pen,6,12,18,12);g.DrawLine(pen,12,6,12,18);break;
        }
        g.Restore(state);
    }
    internal static void Style(Control control)
    {
        control.ForeColor=Text; control.BackColor=Surface;
        if(control is TextBox tb){tb.BackColor=Input;tb.BorderStyle=BorderStyle.FixedSingle;}
        if(control is ComboBox cb){cb.BackColor=Input;cb.FlatStyle=FlatStyle.Flat;}
        if(control is NumericUpDown number){number.BackColor=Input;number.BorderStyle=BorderStyle.FixedSingle;}
    }
    internal static Label Label(string text) => new(){Text=text,AutoSize=true,ForeColor=Muted,Anchor=AnchorStyles.Left,Margin=new Padding(8,0,8,0)};
}
internal sealed class RadioButton : Button
{
    internal RadioGlyph Glyph { get; set; }
    internal Color GlyphColor {get;set;}=WorkspaceTheme.Text;
    internal bool Active {get;set;}
    private bool hover;
    internal RadioButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=WorkspaceTheme.Surface;ForeColor=WorkspaceTheme.Text;Size=new Size(110,36);Margin=new Padding(4);Cursor=Cursors.Hand;UseVisualStyleBackColor=false;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=WorkspaceTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),4*DeviceDpi/96f);
        var fill=Active?Color.FromArgb(27,64,44):hover?Color.FromArgb(35,54,70):BackColor;
        using var b=new LinearGradientBrush(ClientRectangle,ControlPaint.Light(fill,.07f),fill,90);e.Graphics.FillPath(b,path);
        using var p=new Pen(Active?WorkspaceTheme.Green:WorkspaceTheme.Border);e.Graphics.DrawPath(p,path);
        float scale=DeviceDpi/96f;int icon=(int)(22*scale);int gap=Glyph==RadioGlyph.None?0:(int)(10*scale);int textWidth=string.IsNullOrEmpty(Text)?0:TextRenderer.MeasureText(Text,Font).Width;
        int total=textWidth+(Glyph==RadioGlyph.None?0:icon)+(textWidth>0?gap:0);int x=Math.Max((int)(8*scale),(Width-total)/2);
        if(Glyph!=RadioGlyph.None){WorkspaceTheme.Glyph(e.Graphics,Glyph,new RectangleF(x,(Height-icon)/2,icon,icon),Enabled?GlyphColor:WorkspaceTheme.Muted);x+=icon+gap;}
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(x,0,Math.Max(0,Width-x-5),Height),Enabled?ForeColor:WorkspaceTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),ForeColor,fill);
    }
}
internal sealed class RadioSlider : Control
{
    private int min,max=100,value;
    public int Minimum{get=>min;set{min=value;Value=this.value;}}
    public int Maximum{get=>max;set{max=value;Value=this.value;}}
    public int TickFrequency{get;set;}
    public int Value{get=>value;set{int next=Math.Clamp(value,min,Math.Max(min,max));if(this.value==next)return;this.value=next;Invalidate();ValueChanged?.Invoke(this,EventArgs.Empty);}}
    public event EventHandler? ValueChanged;
    public RadioSlider(){DoubleBuffered=true;Size=new Size(180,36);TabStop=true;Cursor=Cursors.Hand;BackColor=WorkspaceTheme.Surface;AccessibleRole=AccessibleRole.Slider;}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float x=10+(Width-20)*(value-min)/(float)Math.Max(1,max-min);using var track=new Pen(WorkspaceTheme.Border,5){StartCap=LineCap.Round,EndCap=LineCap.Round};using var active=new Pen(WorkspaceTheme.Blue,5){StartCap=LineCap.Round,EndCap=LineCap.Round};e.Graphics.DrawLine(track,10,Height/2,Math.Max(10,Width-10),Height/2);if(x>10)e.Graphics.DrawLine(active,10,Height/2,x,Height/2);using var b=new SolidBrush(WorkspaceTheme.Blue);e.Graphics.FillEllipse(b,x-7,Height/2-7,14,14);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-2,-2));}
    private void SetPointer(int x)=>Value=min+(int)Math.Round(Math.Clamp((x-10d)/Math.Max(1,Width-20),0,1)*(max-min));
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();Capture=true;SetPointer(e.X);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&e.Button==MouseButtons.Left)SetPointer(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){Capture=false;base.OnMouseUp(e);}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode is Keys.Left or Keys.Down){Value--;e.Handled=true;}if(e.KeyCode is Keys.Right or Keys.Up){Value++;e.Handled=true;}}
}
internal sealed class RadioMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){using var b=new SolidBrush(WorkspaceTheme.Surface);e.Graphics.FillRectangle(b,e.AffectedBounds);}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){if(e.Item.Selected){using var b=new SolidBrush(WorkspaceTheme.Border);e.Graphics.FillRectangle(b,new Rectangle(Point.Empty,e.Item.Size));}}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=WorkspaceTheme.Text;base.OnRenderItemText(e);}
}
internal sealed class RadioBrand : Control
{
    internal RadioBrand(){Size=new Size(170,44);DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(WorkspaceTheme.Surface);WorkspaceTheme.Glyph(e.Graphics,RadioGlyph.Wave,new RectangleF(4,10,28,28),WorkspaceTheme.Green);using var font=new Font("Segoe UI",20,FontStyle.Bold);TextRenderer.DrawText(e.Graphics,"Kiwi",font,new Point(40,5),WorkspaceTheme.Text);TextRenderer.DrawText(e.Graphics,"DX",font,new Point(98,5),WorkspaceTheme.Green);}
}

internal class RadioComboBox : ComboBox
{
    internal RadioComboBox(){DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;BackColor=WorkspaceTheme.Input;ForeColor=WorkspaceTheme.Text;ItemHeight=24;}
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var b=new SolidBrush((e.State&DrawItemState.Selected)!=0?WorkspaceTheme.Border:WorkspaceTheme.Input);e.Graphics.FillRectangle(b,e.Bounds);
        var text=e.Index>=0&&e.Index<Items.Count?GetItemText(Items[e.Index]):Text;
        TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(e.Bounds.X+5,e.Bounds.Y,e.Bounds.Width-8,e.Bounds.Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if((m.Msg==0xF||m.Msg==0x317||m.Msg==0x318)&&IsHandleCreated)
        {
            using var g=m.Msg==0xF?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam);using var b=new SolidBrush(WorkspaceTheme.Surface);int w=Math.Max(18,(int)(20*DeviceDpi/96f));g.FillRectangle(b,Width-w,0,w,Height);
            using var edge=new Pen(WorkspaceTheme.Border);g.DrawRectangle(edge,0,0,Width-1,Height-1);using var arrow=new SolidBrush(WorkspaceTheme.Muted);int x=Width-w/2,y=Height/2;g.FillPolygon(arrow,new[]{new Point(x-4,y-2),new Point(x+4,y-2),new Point(x,y+2)});
        }
    }
}

internal sealed class RadioTabPage : Panel { public RadioTabPage(string text){Text=text;Dock=DockStyle.Fill;} }
internal sealed class RadioTabs : UserControl
{
    private readonly TableLayoutPanel strip=new(){Dock=DockStyle.Top,Height=40,RowCount=1,Margin=Padding.Empty};
    private readonly Panel body=new(){Dock=DockStyle.Fill,BackColor=WorkspaceTheme.Background};
    private readonly List<RadioButton> buttons=new();
    internal sealed class Pages : System.Collections.ObjectModel.Collection<RadioTabPage>
    {
        private readonly RadioTabs owner;
        internal Pages(RadioTabs owner)=>this.owner=owner;
        internal RadioTabPage? this[string name]=>this.FirstOrDefault(p=>p.Name==name);
        protected override void InsertItem(int index,RadioTabPage item){base.InsertItem(index,item);owner.AddPage(item);}
    }
    internal Pages TabPages{get;}
    private int selected;
    internal int SelectedIndex{get=>selected;set{if(value<0||value>=TabPages.Count)return;selected=value;RefreshSelection();SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}}
    internal RadioTabPage? SelectedTab=>selected<TabPages.Count?TabPages[selected]:null;
    internal event EventHandler? SelectedIndexChanged;
    internal RadioTabs(){Dock=DockStyle.Fill;BackColor=WorkspaceTheme.Background;TabPages=new Pages(this);Controls.Add(body);Controls.Add(strip);}
    private void AddPage(RadioTabPage page)
    {
        int index=buttons.Count;var button=new RadioButton{Text=page.Text,Dock=DockStyle.Fill,Margin=new Padding(0,0,4,0)};
        button.Click+=(_,_)=>SelectedIndex=index;page.TextChanged+=(_,_)=>button.Text=page.Text;
        buttons.Add(button);strip.ColumnCount=buttons.Count;strip.ColumnStyles.Clear();foreach(var b in buttons)strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/buttons.Count));strip.Controls.Add(button,index,0);body.Controls.Add(page);RefreshSelection();
    }
    private void RefreshSelection(){for(int i=0;i<TabPages.Count;i++){TabPages[i].Visible=i==selected;buttons[i].Active=i==selected;buttons[i].Invalidate();}}
}
