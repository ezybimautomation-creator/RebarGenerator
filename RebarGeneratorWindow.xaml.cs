// RebarGeneratorWindow.xaml.cs
// Code-behind for the 4-tab Structural Rebar Designer WPF Window.
// All logic mirrors the Python ParametricRebarWindow class 1-to-1.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitUI = Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json.Linq;
using ToolsByGimhan.RebarGenerator.Helpers;
using ToolsByGimhan.RebarGenerator.Models;
// Aliases to resolve WPF vs Revit DB name clashes
using WpfColor = System.Windows.Media.Color;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfLine = System.Windows.Shapes.Line;
using RevitTransform = Autodesk.Revit.DB.Transform;

// Suppress nullable suggestion to keep code readable side-by-side with Python source
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604

namespace ToolsByGimhan.RebarGenerator
{
    public partial class RebarGeneratorWindow : System.Windows.Window
    {
        // ── Fields ───────────────────────────────────────────────────────
        private readonly RevitUI.UIDocument   _uidoc;
        private readonly Document     _doc;

        public  List<(string name, double diam)> BarTypesData     { get; private set; } = new();
        public  List<(string name, double diam)> StirrupTypesData { get; private set; } = new();
        public  ProfileManager                   Pm               { get; private set; }

        private double _beamWFt = 1.0;
        private double _beamHFt = 2.0;
        private double _scale   = 1.0;

        public  string CurrentZone { get; private set; } = "End Sections";

        public Dictionary<string, Dictionary<string, int>> ZoneConfigs { get; } = new()
        {
            ["End Sections"]   = new() { ["T1"]=2,["T2"]=0,["B1"]=2,["B2"]=0 },
            ["Middle Section"] = new() { ["T1"]=2,["T2"]=0,["B1"]=2,["B2"]=0 }
        };

        public  Dictionary<string, List<RebarPoint>> Bars { get; } = new()
        {
            ["End Sections"]   = new(),
            ["Middle Section"] = new()
        };

        private List<double> _currentSpacers = new();
        public  int SelectedTabIndex { get; private set; }

        // ── Custom Column Ties State ─────────────────────────────────────
        public List<List<int>> CustomColTies { get; } = new();
        private List<int> _selectedColBars = new();
        private List<System.Windows.Point> _drawnColBarCenters = new();

        // ── Constructor ──────────────────────────────────────────────────
        public RebarGeneratorWindow(RevitUI.UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc   = uidoc.Document;
            InitializeComponent();
        }

        // ── Public setup (mirrors Python setup_data) ─────────────────────
        public void SetupData(
            List<(string name, double diam)> barTypes,
            List<(string name, double diam)> stirrupTypes,
            ProfileManager pm)
        {
            BarTypesData     = barTypes;
            StirrupTypesData = stirrupTypes;
            Pm               = pm;

            PopulateAllCombos();

            // Beam events
            top_bar_type_selector.SelectionChanged += GlobalInputChanged;
            bot_bar_type_selector.SelectionChanged += GlobalInputChanged;
            stirrup_selector     .SelectionChanged += GlobalInputChanged;
            side_cover           .TextChanged       += GlobalInputChanged;
            end_spacing          .TextChanged       += GlobalInputChanged;
            mid_spacing          .TextChanged       += GlobalInputChanged;
            beam_width_ui        .TextChanged       += GlobalInputChanged;
            beam_height_ui       .TextChanged       += GlobalInputChanged;
            beam_side_type       .SelectionChanged  += GlobalInputChanged;
            beam_side_qty        .SelectionChanged  += GlobalInputChanged;
            spacer_type          .SelectionChanged  += GlobalInputChanged;
            spacer_spacing       .TextChanged       += GlobalInputChanged;
            top_L2_type          .SelectionChanged  += GlobalInputChanged;
            bot_L2_type          .SelectionChanged  += GlobalInputChanged;
            chk_top_same         .Click             += ToggleBarTypes;
            chk_bot_same         .Click             += ToggleBarTypes;

            foreach (var cb in new[] { top_L1_qty, top_L2_qty, bot_L1_qty, bot_L2_qty })
                cb.SelectionChanged += ZoneInputChanged;

            // Profile buttons
            profile_selector     .SelectionChanged += ProfileChanged;
            save_profile_btn     .Click            += SaveProfileClick;
            delete_profile_btn   .Click            += DeleteProfileClick;

            // Footing
            footing_profile_selector  .SelectionChanged += FootingProfileChanged;
            save_footing_profile_btn  .Click            += SaveFootingProfileClick;
            del_footing_profile_btn   .Click            += DeleteFootingProfileClick;

            // Column
            col_width_ui   .TextChanged      += ColInputChanged;
            col_depth_ui   .TextChanged      += ColInputChanged;
            col_cover      .TextChanged      += ColInputChanged;
            col_corner_type.SelectionChanged += ColInputChanged;
            col_side_x_type.SelectionChanged += ColInputChanged;
            col_side_x_qty .SelectionChanged += ColInputChanged;
            col_side_y_type.SelectionChanged += ColInputChanged;
            col_side_y_qty .SelectionChanged += ColInputChanged;
            col_tie_type   .SelectionChanged += ColInputChanged;
            col_profile_selector.SelectionChanged += ColProfileChanged;
            save_col_profile_btn.Click            += SaveColProfileClick;
            del_col_profile_btn .Click            += DeleteColProfileClick;

            UpdateZoningInfo();
            LoadInitialProfile();
            CalculateAllZones();
            ToggleBarTypes(null, null);
            DrawColPreview();
        }

        // ── Combo population ─────────────────────────────────────────────
        private void PopulateAllCombos()
        {
            // Beam qty combos
            foreach (var cb in new[] { top_L1_qty, top_L2_qty, bot_L1_qty, bot_L2_qty })
            {
                cb.Items.Clear();
                for (int i = 0; i <= 10; i++) cb.Items.Add(i.ToString());
                cb.SelectedIndex = 0;
            }
            beam_side_qty.Items.Clear();
            for (int i = 0; i <= 10; i += 2) beam_side_qty.Items.Add(i.ToString());
            beam_side_qty.SelectedIndex = 0;

            // Bar type combos
            foreach (var cb in new[] { top_bar_type_selector, top_L2_type, bot_bar_type_selector,
                                       bot_L2_type, beam_side_type })
            {
                cb.Items.Clear();
                foreach (var d in BarTypesData) cb.Items.Add(d.name);
                if (cb.Items.Count > 0) cb.SelectedIndex = 0;
            }
            stirrup_selector.Items.Clear();
            foreach (var d in StirrupTypesData) stirrup_selector.Items.Add(d.name);
            if (stirrup_selector.Items.Count > 0) stirrup_selector.SelectedIndex = 0;

            spacer_type.Items.Clear();
            spacer_type.Items.Add("None");
            foreach (var d in BarTypesData) spacer_type.Items.Add(d.name);
            spacer_type.SelectedIndex = 0;

            // Column combos
            foreach (var cb in new[] { col_corner_type, col_side_x_type, col_side_y_type })
            {
                cb.Items.Clear();
                foreach (var d in BarTypesData) cb.Items.Add(d.name);
                if (cb.Items.Count > 0) cb.SelectedIndex = 0;
            }
            col_tie_type.Items.Clear();
            foreach (var d in StirrupTypesData) col_tie_type.Items.Add(d.name);
            if (col_tie_type.Items.Count > 0) col_tie_type.SelectedIndex = 0;

            foreach (var cb in new[] { col_side_x_qty, col_side_y_qty })
            {
                cb.Items.Clear();
                for (int i = 0; i <= 10; i++) cb.Items.Add(i.ToString());
                cb.SelectedIndex = 0;
            }

            foreach (var cb in new[] { footing_dowel_qx, footing_dowel_qy })
            {
                cb.Items.Clear();
                for (int i = 0; i <= 20; i++) cb.Items.Add(i.ToString());
                if (cb.Items.Count > 2) cb.SelectedIndex = 2; // Default to 2
            }

            // Footing combos — prefer a "12" type if available
            foreach (var cb in new[] { footing_bx_type, footing_by_type, footing_tx_type, footing_ty_type, footing_dowel_type })
            {
                cb.Items.Clear();
                int idx12 = 0;
                for (int i = 0; i < BarTypesData.Count; i++)
                {
                    cb.Items.Add(BarTypesData[i].name);
                    if (BarTypesData[i].name.Contains("12")) idx12 = i;
                }
                cb.SelectedIndex = idx12;
            }

            RefreshProfileSelectors();
        }

        private void RefreshProfileSelectors()
        {
            profile_selector       .Items.Clear();
            col_profile_selector   .Items.Clear();
            footing_profile_selector.Items.Clear();

            profile_selector       .Items.Add("Default");
            col_profile_selector   .Items.Add("Default");
            footing_profile_selector.Items.Add("Default");

            foreach (var kv in Pm.Profiles.OrderBy(k => k.Key))
            {
                if (kv.Key == "Default") continue;
                string pType = kv.Value["Type"]?.Value<string>() ?? "";
                switch (pType)
                {
                    case "Column":  col_profile_selector   .Items.Add(kv.Key); break;
                    case "Footing": footing_profile_selector.Items.Add(kv.Key); break;
                    default:        profile_selector       .Items.Add(kv.Key); break;
                }
            }
            profile_selector       .SelectedIndex = 0;
            col_profile_selector   .SelectedIndex = 0;
            footing_profile_selector.SelectedIndex = 0;
        }

        // ── Zone management ──────────────────────────────────────────────
        private void UpdateZoningInfo()
        {
            zone_title.Text = "Editing: " + CurrentZone;
            switch_zone_btn.Content = CurrentZone == "End Sections"
                ? "Switch to Middle Section" : "Switch to End Sections";
        }

        public void SwitchZoneClick(object sender, RoutedEventArgs e)
        {
            UpdateZoneConfigFromUI(CurrentZone);
            CurrentZone = CurrentZone == "End Sections" ? "Middle Section" : "End Sections";
            UpdateZoningInfo();
            LoadZoneUIFromConfig(CurrentZone);
            CalculateBars(CurrentZone);
        }

        private void UpdateZoneConfigFromUI(string zone)
        {
            int Q(ComboBox cb) { int.TryParse(cb.SelectedItem as string, out int v); return v; }
            ZoneConfigs[zone] = new Dictionary<string, int>
            {
                ["T1"] = Q(top_L1_qty), ["T2"] = Q(top_L2_qty),
                ["B1"] = Q(bot_L1_qty), ["B2"] = Q(bot_L2_qty)
            };
        }

        private void LoadZoneUIFromConfig(string zone)
        {
            var cfg = ZoneConfigs[zone];
            top_L1_qty.SelectedItem = cfg.GetValueOrDefault("T1", 2).ToString();
            top_L2_qty.SelectedItem = cfg.GetValueOrDefault("T2", 0).ToString();
            bot_L1_qty.SelectedItem = cfg.GetValueOrDefault("B1", 2).ToString();
            bot_L2_qty.SelectedItem = cfg.GetValueOrDefault("B2", 0).ToString();
        }

        // ── Input-change handlers ────────────────────────────────────────
        private void GlobalInputChanged(object sender, EventArgs e)
        {
            if (double.TryParse(beam_width_ui .Text, out double bw)) _beamWFt = bw  / 304.8;
            if (double.TryParse(beam_height_ui.Text, out double bh)) _beamHFt = bh  / 304.8;
            CalculateAllZones();
        }

        private void ZoneInputChanged(object sender, EventArgs e)
        {
            UpdateZoneConfigFromUI(CurrentZone);
            CalculateBars(CurrentZone);
        }

        private void ColInputChanged(object sender, EventArgs e)
        {
            CustomColTies.Clear();
            _selectedColBars.Clear();
            DrawColPreview();
        }

        private void ToggleBarTypes(object sender, RoutedEventArgs e)
        {
            if (chk_top_same.IsChecked == true)
                top_L2_type.SelectedIndex = top_bar_type_selector.SelectedIndex;
            if (chk_bot_same.IsChecked == true)
                bot_L2_type.SelectedIndex = bot_bar_type_selector.SelectedIndex;

            top_L2_type.Visibility = chk_top_same.IsChecked == true ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            bot_L2_type.Visibility = chk_bot_same.IsChecked == true ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            CalculateAllZones();
        }

        // ── Bar calculation ──────────────────────────────────────────────
        private void CalculateAllZones()
        {
            foreach (var z in new[] { "End Sections", "Middle Section" }) CalculateBars(z);
        }

        private double GetDiam(string name, bool isStirrup = false)
        {
            var src = isStirrup ? StirrupTypesData : BarTypesData;
            return src.FirstOrDefault(d => d.name == name).diam;
        }

        private void CalculateBars(string zone)
        {
            try
            {
                string ts  = top_bar_type_selector.SelectedItem as string ?? "";
                string ts2 = chk_top_same.IsChecked == true ? ts : (top_L2_type.SelectedItem as string ?? ts);
                string bs  = bot_bar_type_selector.SelectedItem as string ?? "";
                string bs2 = chk_bot_same.IsChecked == true ? bs : (bot_L2_type.SelectedItem as string ?? bs);
                string ss  = stirrup_selector.SelectedItem as string ?? "";

                double td  = GetDiam(ts);  if (td  == 0) td  = 0.02;
                double td2 = GetDiam(ts2); if (td2 == 0) td2 = td;
                double bd  = GetDiam(bs);  if (bd  == 0) bd  = 0.02;
                double bd2 = GetDiam(bs2); if (bd2 == 0) bd2 = bd;
                double sd  = GetDiam(ss, true); if (sd == 0) sd = 0.01;

                double.TryParse(side_cover.Text, out double covMm);
                double cov = covMm / 304.8;

                var cfg = ZoneConfigs[zone];
                var nb  = new List<RebarPoint>();
                var spacers = new List<double>();

                // TOP BARS
                double ewT  = _beamWFt - 2*cov - 2*sd - td;
                double sxT  = -_beamWFt/2 + cov + sd + td/2;
                if (ewT > 0)
                {
                    double yt1 = _beamHFt/2 - cov - sd - td/2;
                    AddBars(nb, cfg.GetValueOrDefault("T1"), yt1, td, ts, sxT, ewT);

                    if (cfg.GetValueOrDefault("T2") > 0)
                    {
                        double ewT2 = _beamWFt - 2*cov - 2*sd - td2;
                        double sxT2 = -_beamWFt/2 + cov + sd + td2/2;
                        double gap  = Math.Max(Math.Max(td, td2), 0.082);
                        double yt2  = yt1 - td/2 - gap - td2/2;
                        if (ewT2 > 0) AddBars(nb, cfg.GetValueOrDefault("T2"), yt2, td2, ts2, sxT2, ewT2);
                        if (spacer_type.SelectedItem as string != "None")
                            spacers.Add(yt1 - td/2 - gap/2);
                    }
                }

                // BOTTOM BARS
                double ewB  = _beamWFt - 2*cov - 2*sd - bd;
                double sxB  = -_beamWFt/2 + cov + sd + bd/2;
                if (ewB > 0)
                {
                    double yb1 = -_beamHFt/2 + cov + sd + bd/2;
                    AddBars(nb, cfg.GetValueOrDefault("B1"), yb1, bd, bs, sxB, ewB);

                    if (cfg.GetValueOrDefault("B2") > 0)
                    {
                        double ewB2 = _beamWFt - 2*cov - 2*sd - bd2;
                        double sxB2 = -_beamWFt/2 + cov + sd + bd2/2;
                        double gap  = Math.Max(Math.Max(bd, bd2), 0.082);
                        double yb2  = yb1 + bd/2 + gap + bd2/2;
                        if (ewB2 > 0) AddBars(nb, cfg.GetValueOrDefault("B2"), yb2, bd2, bs2, sxB2, ewB2);
                        if (spacer_type.SelectedItem as string != "None")
                            spacers.Add(yb1 + bd/2 + gap/2);
                    }
                }

                // SIDE BARS
                if (int.TryParse(beam_side_qty.SelectedItem as string, out int sq) && sq > 0)
                {
                    string stN = beam_side_type.SelectedItem as string ?? "";
                    double sidD = GetDiam(stN); if (sidD == 0) sidD = 0.01;
                    double sInH = _beamHFt - 2*cov - 2*sd;
                    if (sInH > 0)
                    {
                        int qSide = sq / 2;
                        double step = sInH / (qSide + 1);
                        double yS   = -sInH/2.0 + step;
                        double xOff = _beamWFt/2.0 - cov - sd - sidD/2.0;
                        for (int i = 0; i < qSide; i++)
                        {
                            double locY = yS + i*step;
                            nb.Add(new RebarPoint(-xOff, locY, sidD, stN));
                            nb.Add(new RebarPoint( xOff, locY, sidD, stN));
                        }
                    }
                }

                Bars[zone] = nb;
                if (zone == CurrentZone) { _currentSpacers = spacers; DrawUI(); }
            }
            catch { /* silent—preview only */ }
        }

        private static void AddBars(List<RebarPoint> list, int qty, double y,
                                    double diam, string typeName, double sx, double ew)
        {
            if (qty <= 0) return;
            if (qty == 1) { list.Add(new RebarPoint(0, y, diam, typeName)); return; }
            double sp = ew / (qty - 1);
            for (int i = 0; i < qty; i++)
                list.Add(new RebarPoint(sx + i*sp, y, diam, typeName));
        }

        // ── Beam canvas drawing ──────────────────────────────────────────
        private void DrawUI()
        {
            rebar_canvas.Children.Clear();

            double cw = rebar_canvas.Width, ch = rebar_canvas.Height;
            double aspect = _beamWFt / Math.Max(_beamHFt, 0.001);
            _scale = aspect > (cw/ch) ? cw / _beamWFt : ch / _beamHFt;
            double bw = _beamWFt * _scale, bh = _beamHFt * _scale;
            double cx = cw/2, cy = ch/2;

            // Beam outline
            AddRect(rebar_canvas, cx - bw/2, cy - bh/2, bw, bh, Brushes.Black, 2,
                    new SolidColorBrush(WpfColor.FromArgb(15,0,0,0)));

            // Stirrup
            double.TryParse(side_cover.Text, out double covMm);
            double cov = covMm / 304.8;
            double sw = bw - 2*cov*_scale, sh = bh - 2*cov*_scale;
            if (sw > 0 && sh > 0)
            {
                string ss = stirrup_selector.SelectedItem as string ?? "";
                double sd_ = GetDiam(ss, true); if (sd_ == 0) sd_ = 0.01;
                double thk = Math.Max(sd_ * _scale, 2.0);
                AddRect(rebar_canvas, cx - sw/2, cy - sh/2, sw, sh, Brushes.Red, thk, null);

                // Spacers
                foreach (double spy in _currentSpacers)
                {
                    var line = new System.Windows.Shapes.Rectangle
                        { Width = sw, Height = 2, Fill = Brushes.Green };
                    Canvas.SetLeft(line, cx - sw/2);
                    Canvas.SetTop (line, cy - spy * _scale - 1);
                    rebar_canvas.Children.Add(line);
                }
            }

            // Bars
            foreach (var rb in Bars[CurrentZone])
            {
                double dia = Math.Max(rb.DiameterFt * _scale, 4);
                var ell = new WpfEllipse { Width = dia, Height = dia,
                    Fill = Brushes.SteelBlue, Stroke = Brushes.White, StrokeThickness = 1 };
                ell.ToolTip = rb.TypeName;
                Canvas.SetLeft(ell, cx + rb.Lx * _scale - dia/2);
                Canvas.SetTop (ell, cy - rb.Ly * _scale - dia/2);
                rebar_canvas.Children.Add(ell);
            }
        }

        // ── Custom Column Ties Interaction ───────────────────────────────
        private void AddClickableColBar(double cx, double cy, double diaDraw, Brush fill, int barIndex)
        {
            double d = Math.Max(diaDraw, 5.0); // Minimum clickable area
            var e = new WpfEllipse { Width = d, Height = d, Fill = fill, Tag = barIndex };
            if (_selectedColBars.Contains(barIndex))
            {
                e.Stroke = Brushes.Orange;
                e.StrokeThickness = Math.Max(2.0, d * 0.2);
            }
            Canvas.SetLeft(e, cx - d / 2); Canvas.SetTop(e, cy - d / 2);
            col_preview_canvas.Children.Add(e);
        }

        private void ColCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is WpfEllipse ell && ell.Tag is int idx)
            {
                if (_selectedColBars.Contains(idx)) _selectedColBars.Remove(idx);
                else _selectedColBars.Add(idx);
                DrawColPreview();
            }
            else
            {
                _selectedColBars.Clear();
                DrawColPreview();
            }
        }

        private void AddTie_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedColBars.Count >= 3)
            {
                CustomColTies.Add(new List<int>(_selectedColBars));
                _selectedColBars.Clear();
                DrawColPreview();
            }
            else
            {
                Msg("Please select at least 3 bars to form a closed tie loop.");
            }
        }

        private void ClearTies_Click(object sender, RoutedEventArgs e)
        {
            CustomColTies.Clear();
            _selectedColBars.Clear();
            DrawColPreview();
        }

        // ── Column canvas preview ────────────────────────────────────────
        private void DrawColPreview()
        {
            col_preview_canvas.Children.Clear();
            _drawnColBarCenters.Clear();
            try
            {
                if (!double.TryParse(col_width_ui.Text, out double cwMm) || cwMm <= 0) return;
                if (!double.TryParse(col_depth_ui.Text, out double cdMm) || cdMm <= 0) return;
                if (!double.TryParse(col_cover.Text,    out double covMm))              return;

                double av = 240.0, ah = 200.0;
                double sc = Math.Min(av / cwMm, ah / cdMm) * 0.9;
                double dw = cwMm * sc, dh = cdMm * sc;
                double ox = (260 - dw) / 2, oy = (220 - dh) / 2;

                string tieN = col_tie_type.SelectedItem as string ?? "";
                double tieD = GetDiam(tieN, true) * 304.8;
                string cornN = col_corner_type.SelectedItem as string ?? "";
                double cornD = GetDiam(cornN) * 304.8;
                string sxN   = col_side_x_type.SelectedItem as string ?? "";
                double sxD   = GetDiam(sxN) * 304.8;
                string syN   = col_side_y_type.SelectedItem as string ?? "";
                double syD   = GetDiam(syN) * 304.8;

                double ins = (covMm + tieD + cornD / 2) * sc;
                
                // 1. Calculate physical centers in sequential order
                _drawnColBarCenters.Add(new System.Windows.Point(ox + ins, oy + ins)); // 0 = TL
                _drawnColBarCenters.Add(new System.Windows.Point(ox + dw - ins, oy + ins)); // 1 = TR
                _drawnColBarCenters.Add(new System.Windows.Point(ox + dw - ins, oy + dh - ins)); // 2 = BR
                _drawnColBarCenters.Add(new System.Windows.Point(ox + ins, oy + dh - ins)); // 3 = BL
                List<double> barDiameters = new() { cornD, cornD, cornD, cornD };

                if (int.TryParse(col_side_x_qty.SelectedItem as string, out int qx) && qx > 0)
                {
                    double step = (dw - 2 * ins) / (qx + 1);
                    for (int i = 1; i <= qx; i++)
                    {
                        _drawnColBarCenters.Add(new System.Windows.Point(ox + ins + i * step, oy + ins)); barDiameters.Add(sxD); // Top Face
                        _drawnColBarCenters.Add(new System.Windows.Point(ox + ins + i * step, oy + dh - ins)); barDiameters.Add(sxD); // Bottom Face
                    }
                }
                if (int.TryParse(col_side_y_qty.SelectedItem as string, out int qy) && qy > 0)
                {
                    double step = (dh - 2 * ins) / (qy + 1);
                    for (int i = 1; i <= qy; i++)
                    {
                        _drawnColBarCenters.Add(new System.Windows.Point(ox + ins, oy + ins + i * step)); barDiameters.Add(syD); // Left Face
                        _drawnColBarCenters.Add(new System.Windows.Point(ox + dw - ins, oy + ins + i * step)); barDiameters.Add(syD); // Right Face
                    }
                }

                // 2. Draw Column Outline & Outer Perimeter Tie
                AddRect(col_preview_canvas, ox, oy, dw, dh, Brushes.Gray, 2, Brushes.WhiteSmoke);
                double stW = Math.Max(0, cwMm - 2 * covMm) * sc;
                double stH = Math.Max(0, cdMm - 2 * covMm) * sc;
                if (stW > 0 && stH > 0)
                {
                    AddRect(col_preview_canvas, ox + covMm * sc, oy + covMm * sc, stW, stH, Brushes.Green, Math.Max(tieD * sc, 2.0), null);
                }

                // 3. Draw Custom Ties as POLYGONS through selected bar centers
                foreach (var group in CustomColTies)
                {
                    if (group.Count < 3) continue;
                    // Collect bar center canvas positions
                    var pts = new List<System.Windows.Point>();
                    foreach (int idx in group)
                    {
                        if (idx >= _drawnColBarCenters.Count) continue;
                        var p = _drawnColBarCenters[idx];
                        bool dup = false;
                        foreach (var u in pts) if (Math.Abs(u.X - p.X) < 0.5 && Math.Abs(u.Y - p.Y) < 0.5) { dup = true; break; }
                        if (!dup) pts.Add(p);
                    }
                    if (pts.Count < 3) continue;

                    // Center & sort clockwise by angle
                    double cx2 = 0, cy2 = 0;
                    foreach (var p in pts) { cx2 += p.X; cy2 += p.Y; }
                    cx2 /= pts.Count; cy2 /= pts.Count;
                    pts.Sort((a, b) => Math.Atan2(b.Y - cy2, b.X - cx2).CompareTo(Math.Atan2(a.Y - cy2, a.X - cx2)));

                    // Draw polygon
                    var poly = new System.Windows.Shapes.Polygon
                    {
                        Stroke = Brushes.Orange,
                        StrokeThickness = Math.Max(tieD * sc, 2.0),
                        Fill = null
                    };
                    foreach (var p in pts)
                        poly.Points.Add(p);
                    col_preview_canvas.Children.Add(poly);
                }

                // 4. Draw Clickable Bars on top
                for (int i = 0; i < _drawnColBarCenters.Count; i++)
                {
                    Brush fill = (i < 4) ? Brushes.Red : Brushes.Blue; // Corners red, sides blue
                    AddClickableColBar(_drawnColBarCenters[i].X, _drawnColBarCenters[i].Y, barDiameters[i] * sc, fill, i);
                }

                // 5. Update UI Status Message
                if (tie_status_text != null)
                {
                    tie_status_text.Text = _selectedColBars.Count > 0 
                        ? $"{_selectedColBars.Count} bars selected. Click 'Add Tie' to combine."
                        : $"{CustomColTies.Count} custom ties drawn. Click bars to build more.";
                }
            }
            catch { /* preview only */ }
        }

        // ── Profile management – Beam ────────────────────────────────────
        private void LoadInitialProfile()
        {
            profile_selector.SelectedIndex = profile_selector.Items.IndexOf("Default") >= 0
                ? profile_selector.Items.IndexOf("Default") : 0;
            LoadZoneUIFromConfig(CurrentZone);
        }

        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            string name = profile_selector.SelectedItem as string ?? "";
            if (!Pm.Profiles.TryGetValue(name, out var data)) return;
            var g = data["Global"] as JObject;

            Try(() =>
            {
                side_cover    .Text = ProfileManager.GetVal(g, "SideCover",  25).ToString();
                end_spacing   .Text = ProfileManager.GetVal(g, "EndSpacing", 150).ToString();
                mid_spacing   .Text = ProfileManager.GetVal(g, "MidSpacing", 250).ToString();
                beam_width_ui .Text = ProfileManager.GetVal(g, "BeamWidth",  300).ToString();
                beam_height_ui.Text = ProfileManager.GetVal(g, "BeamHeight", 600).ToString();

                string mainT = ProfileManager.GetVal<string>(g, "BarType", null);
                SetCb(top_bar_type_selector, ProfileManager.GetVal<string>(g, "TopBarType", mainT));
                SetCb(bot_bar_type_selector, ProfileManager.GetVal<string>(g, "BotBarType", mainT));
                SetCb(stirrup_selector,      ProfileManager.GetVal<string>(g, "StirrupType", null));
                SetCb(spacer_type,           ProfileManager.GetVal<string>(g, "SpacerType", "None"));
                spacer_spacing.Text = ProfileManager.GetVal(g, "SpacerSpacing", 1000).ToString();
                SetCb(beam_side_type,        ProfileManager.GetVal<string>(g, "SideBarType", mainT));
                SetCb(beam_side_qty,         ProfileManager.GetVal(g, "SideBarQty",0).ToString());

                chk_top_same.IsChecked = ProfileManager.GetVal(g, "TopSame", true);
                chk_bot_same.IsChecked = ProfileManager.GetVal(g, "BotSame", true);
                SetCb(top_L2_type, ProfileManager.GetVal<string>(g, "TopL2Type", null));
                SetCb(bot_L2_type, ProfileManager.GetVal<string>(g, "BotL2Type", null));
                ToggleBarTypes(null, null);
            });

            if (data.ContainsKey("End Sections"))
                ZoneConfigs["End Sections"] = ParseZone(data["End Sections"] as JObject);
            if (data.ContainsKey("Middle Section"))
                ZoneConfigs["Middle Section"] = ParseZone(data["Middle Section"] as JObject);

            LoadZoneUIFromConfig(CurrentZone);
            CalculateAllZones();
        }

        private void SaveProfileClick(object sender, RoutedEventArgs e)
        {
            string name = AskString("Enter Profile Name:", "Save Profile");
            if (string.IsNullOrEmpty(name)) return;
            UpdateZoneConfigFromUI(CurrentZone);

            var data = new JObject
            {
                ["End Sections"]   = ZoneToJObject(ZoneConfigs["End Sections"]),
                ["Middle Section"] = ZoneToJObject(ZoneConfigs["Middle Section"]),
                ["Global"] = new JObject
                {
                    ["SideCover"]    = side_cover.Text,
                    ["EndSpacing"]   = end_spacing.Text,
                    ["MidSpacing"]   = mid_spacing.Text,
                    ["BeamWidth"]    = beam_width_ui.Text,
                    ["BeamHeight"]   = beam_height_ui.Text,
                    ["TopBarType"]   = top_bar_type_selector.SelectedItem as string,
                    ["BotBarType"]   = bot_bar_type_selector.SelectedItem as string,
                    ["BarType"]      = top_bar_type_selector.SelectedItem as string,
                    ["StirrupType"]  = stirrup_selector.SelectedItem as string,
                    ["SpacerType"]   = spacer_type.SelectedItem as string,
                    ["SpacerSpacing"]= spacer_spacing.Text,
                    ["SideBarType"]  = beam_side_type.SelectedItem as string,
                    ["SideBarQty"]   = beam_side_qty.SelectedItem as string,
                    ["TopSame"]      = chk_top_same.IsChecked,
                    ["BotSame"]      = chk_bot_same.IsChecked,
                    ["TopL2Type"]    = top_L2_type.SelectedItem as string,
                    ["BotL2Type"]    = bot_L2_type.SelectedItem as string
                }
            };
            if (Pm.SaveProfile(name, data))
            {
                if (!profile_selector.Items.Contains(name)) profile_selector.Items.Add(name);
                profile_selector.SelectedItem = name;
                Msg("Profile Saved!");
            }
            else Msg("Error saving profile.");
        }

        private void DeleteProfileClick(object sender, RoutedEventArgs e)
        {
            string name = profile_selector.SelectedItem as string ?? "";
            if (name == "Default") { Msg("Cannot delete Default profile."); return; }
            if (Confirm($"Delete profile '{name}'?"))
            {
                Pm.DeleteProfile(name);
                profile_selector.Items.Remove(name);
                if (profile_selector.Items.Count > 0) profile_selector.SelectedIndex = 0;
                Msg("Profile Deleted.");
            }
        }

        // ── Profile management – Column ──────────────────────────────────
        private void ColProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            string name = col_profile_selector.SelectedItem as string ?? "";
            if (name == "Default" || !Pm.Profiles.TryGetValue(name, out var data)) return;
            Try(() =>
            {
                col_width_ui      .Text = ProfileManager.GetVal(data, "ColWidth",  400).ToString();
                col_depth_ui      .Text = ProfileManager.GetVal(data, "ColDepth",  400).ToString();
                col_cover         .Text = ProfileManager.GetVal(data, "ColCover",   25).ToString();
                col_tie_spacing_end.Text = ProfileManager.GetVal(data,"TieSpacingEnd",100).ToString();
                col_tie_spacing_mid.Text = ProfileManager.GetVal(data,"TieSpacingMid",200).ToString();
                col_conf_height   .Text = ProfileManager.GetVal(data, "ConfHeight", 600).ToString();
                col_top_extension .Text = ProfileManager.GetVal(data, "TopExtension",  0).ToString();
                SetCb(col_corner_type,  ProfileManager.GetVal<string>(data,"CornerType",null));
                SetCb(col_side_x_type,  ProfileManager.GetVal<string>(data,"SideXType", null));
                SetCb(col_side_y_type,  ProfileManager.GetVal<string>(data,"SideYType", null));
                SetCb(col_tie_type,     ProfileManager.GetVal<string>(data,"TieType",   null));
                col_side_x_qty.SelectedItem = ProfileManager.GetVal(data,"SideXQty",0).ToString();
                col_side_y_qty.SelectedItem = ProfileManager.GetVal(data,"SideYQty",0).ToString();
            });
        }

        private void SaveColProfileClick(object sender, RoutedEventArgs e)
        {
            string name = AskString("Enter Profile Name:", "Save Column Profile", "Col-Type-A");
            if (string.IsNullOrEmpty(name)) return;
            var data = new JObject
            {
                ["Type"]          = "Column",
                ["ColWidth"]      = col_width_ui.Text,
                ["ColDepth"]      = col_depth_ui.Text,
                ["ColCover"]      = col_cover.Text,
                ["TieSpacingEnd"] = col_tie_spacing_end.Text,
                ["TieSpacingMid"] = col_tie_spacing_mid.Text,
                ["ConfHeight"]    = col_conf_height.Text,
                ["TopExtension"]  = col_top_extension.Text,
                ["CornerType"]    = col_corner_type.SelectedItem as string,
                ["SideXType"]     = col_side_x_type.SelectedItem as string,
                ["SideYType"]     = col_side_y_type.SelectedItem as string,
                ["TieType"]       = col_tie_type.SelectedItem as string,
                ["SideXQty"]      = col_side_x_qty.SelectedItem as string,
                ["SideYQty"]      = col_side_y_qty.SelectedItem as string
            };
            Pm.SaveProfile(name, data);
            RefreshProfileSelectors();
            col_profile_selector.SelectedItem = name;
            Msg("Column Profile Saved!");
        }

        private void DeleteColProfileClick(object sender, RoutedEventArgs e)
        {
            string name = col_profile_selector.SelectedItem as string ?? "";
            if (name == "Default") return;
            if (Confirm($"Delete profile '{name}'?"))
            {
                Pm.DeleteProfile(name); RefreshProfileSelectors(); Msg("Profile Deleted.");
            }
        }

        // ── Profile management – Footing ─────────────────────────────────
        private void FootingProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            string name = footing_profile_selector.SelectedItem as string ?? "";
            if (name == "Default" || !Pm.Profiles.TryGetValue(name, out var data)) return;
            Try(() =>
            {
                footing_cover.Text = ProfileManager.GetVal(data,"Cover",50).ToString();
                chk_footing_top_mat.IsChecked = ProfileManager.GetVal(data,"TopMat",false);
                footing_bx_spacing.Text = ProfileManager.GetVal(data,"BXSpacing",200).ToString();
                footing_by_spacing.Text = ProfileManager.GetVal(data,"BYSpacing",200).ToString();
                footing_tx_spacing.Text = ProfileManager.GetVal(data,"TXSpacing",200).ToString();
                footing_ty_spacing.Text = ProfileManager.GetVal(data,"TYSpacing",200).ToString();
                SetCb(footing_bx_type, ProfileManager.GetVal<string>(data,"BXType",null));
                SetCb(footing_by_type, ProfileManager.GetVal<string>(data,"BYType",null));
                SetCb(footing_tx_type, ProfileManager.GetVal<string>(data,"TXType",null));
                SetCb(footing_ty_type, ProfileManager.GetVal<string>(data,"TYType",null));
            });
        }

        private void SaveFootingProfileClick(object sender, RoutedEventArgs e)
        {
            string name = AskString("Enter Footing Profile Name:", "Save Footing Profile", "F-Type-1");
            if (string.IsNullOrEmpty(name)) return;
            var data = new JObject
            {
                ["Type"]      = "Footing",
                ["Cover"]     = footing_cover.Text,
                ["TopMat"]    = chk_footing_top_mat.IsChecked,
                ["BXType"]    = footing_bx_type.SelectedItem as string,
                ["BXSpacing"] = footing_bx_spacing.Text,
                ["BYType"]    = footing_by_type.SelectedItem as string,
                ["BYSpacing"] = footing_by_spacing.Text,
                ["TXType"]    = footing_tx_type.SelectedItem as string,
                ["TXSpacing"] = footing_tx_spacing.Text,
                ["TYType"]    = footing_ty_type.SelectedItem as string,
                ["TYSpacing"] = footing_ty_spacing.Text
            };
            Pm.SaveProfile(name, data);
            RefreshProfileSelectors();
            footing_profile_selector.SelectedItem = name;
            Msg("Footing Profile Saved!");
        }

        private void DeleteFootingProfileClick(object sender, RoutedEventArgs e)
        {
            string name = footing_profile_selector.SelectedItem as string ?? "";
            if (name == "Default") return;
            if (Confirm($"Delete footing profile '{name}'?"))
            {
                Pm.DeleteProfile(name); RefreshProfileSelectors(); Msg("Footing Profile Deleted.");
            }
        }

        // ── Section Tool ─────────────────────────────────────────────────
        public void SecBeamClick(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                var refs = _uidoc.Selection.PickObjects(ObjectType.Element,
                               new CategorySelectionFilter(BuiltInCategory.OST_StructuralFraming), "Select Beams");
                if (refs == null || refs.Count == 0) { this.Show(); return; }

                var vt = GetSectionViewType();
                if (vt == null) { Msg("No Section View Family Type found."); this.Show(); return; }

                bool useCustom = chk_beam_custom.IsChecked == true;
                var customLocs = new List<double>();
                if (useCustom)
                    foreach (var p in txt_beam_locs.Text.Split(','))
                        if (double.TryParse(p.Trim(), out double v)) customLocs.Add(v);

                int cnt = 0;
                using (var t = new Transaction(_doc, "Create Beam Sections"))
                {
                    t.Start();
                    foreach (var r in refs)
                    {
                        var el = _doc.GetElement(r);
                        if (!ElementIdHelper.IsCategory(el, BuiltInCategory.OST_StructuralFraming)) continue;
                        GetElementDataSec(el, out XYZ p0, out XYZ vec);
                        if (p0 == null || vec == null) continue;
                        double len = vec.GetLength();
                        if (len < 0.001 || vec.Normalize().IsAlmostEqualTo(XYZ.BasisZ)) continue;

                        string mark = el.LookupParameter("Mark")?.AsString() ?? el.Id.ToString();
                        var locs = useCustom
                            ? customLocs.Select(l => (l, $" - L={len*304.8*l:F0}")).ToList()
                            : new List<(double, string)> { (1.0/6.0, " - SUPPORT"), (0.5, " - MID") };

                        foreach (var (ratio, suffix) in locs)
                        {
                            var vs = CreateSectionView(_doc, el, p0 + vec*ratio, vec, vt, mark + suffix);
                            if (vs != null)
                            {
                                cnt++;
                                if (chk_dims_beam.IsChecked == true) CreateSectionDims(_doc, vs, el);
                            }
                        }
                    }
                    t.Commit();
                }
                Msg(cnt == 0 ? "No beam sections created." : $"Created {cnt} beam sections.");
            }
            catch { /* user cancelled */ }
            this.Show();
        }

        public void SecColClick(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                var refs = _uidoc.Selection.PickObjects(ObjectType.Element,
                               new CategorySelectionFilter(BuiltInCategory.OST_StructuralColumns), "Select Columns");
                if (refs == null || refs.Count == 0) { this.Show(); return; }

                var vt = GetSectionViewType();
                if (vt == null) { Msg("No Section View Family Type found."); this.Show(); return; }

                bool useCustom = chk_col_custom.IsChecked == true;
                var customLocs = new List<double>();
                if (useCustom)
                    foreach (var p in txt_col_locs.Text.Split(','))
                        if (double.TryParse(p.Trim(), out double v)) customLocs.Add(v);

                int cnt = 0;
                using (var t = new Transaction(_doc, "Create Column Sections"))
                {
                    t.Start();
                    foreach (var r in refs)
                    {
                        var el = _doc.GetElement(r);
                        if (!ElementIdHelper.IsCategory(el, BuiltInCategory.OST_StructuralColumns)) continue;
                        var bb = el.get_BoundingBox(null);
                        if (bb == null) continue;

                        double zMin = bb.Min.Z, zLen = bb.Max.Z - bb.Min.Z;
                        string mark = el.LookupParameter("Mark")?.AsString() ?? el.Id.ToString();
                        var locs = useCustom
                            ? customLocs.Select(l => (l, $" - H={zLen*304.8*l:F0}")).ToList()
                            : new List<(double, string)> { (0.5, " - SECTION") };

                        foreach (var (ratio, suffix) in locs)
                        {
                            double cx2 = (bb.Min.X + bb.Max.X)/2;
                            double cy2 = (bb.Min.Y + bb.Max.Y)/2;
                            var pt  = new XYZ(cx2, cy2, zMin + zLen*ratio);
                            var vs  = CreateSectionView(_doc, el, pt, XYZ.BasisZ, vt, mark + suffix);
                            if (vs != null)
                            {
                                cnt++;
                                if (chk_dims_col.IsChecked == true) CreateSectionDims(_doc, vs, el);
                            }
                        }
                    }
                    t.Commit();
                }
                Msg(cnt == 0 ? "No column sections created." : $"Created {cnt} column sections.");
            }
            catch { /* user cancelled */ }
            this.Show();
        }

        // ── Submit ────────────────────────────────────────────────────────
        public void SubmitClick(object sender, RoutedEventArgs e)
        {
            SelectedTabIndex = main_tab_control.SelectedIndex;
            DialogResult = true;
            Close();
        }

        // ── Section helpers ───────────────────────────────────────────────
        private ViewFamilyType GetSectionViewType()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vt => vt.ViewFamily == ViewFamily.Section);
        }

        private static void GetElementDataSec(Element el, out XYZ p0, out XYZ vec)
        {
            p0 = null; vec = null;
            if (el.Location is LocationCurve lc && lc.Curve is Autodesk.Revit.DB.Line line)
            {
                p0  = line.GetEndPoint(0);
                vec = line.GetEndPoint(1) - p0;
            }
            else if (el.Location is LocationPoint lp)
            {
                p0  = lp.Point;
                vec = XYZ.BasisZ;
            }
        }

        private static ViewSection CreateSectionView(Document doc, Element el,
            XYZ point, XYZ vector, ViewFamilyType vt, string name)
        {
            try
            {
                var normVec   = vector.Normalize();
                bool isVert   = normVec.IsAlmostEqualTo(XYZ.BasisZ) || normVec.IsAlmostEqualTo(-XYZ.BasisZ);
                var t         = RevitTransform.Identity;
                t.Origin      = point;

                if (isVert) { t.BasisZ = XYZ.BasisZ; t.BasisY = XYZ.BasisY; t.BasisX = XYZ.BasisX; }
                else
                {
                    var viewDir = normVec;
                    var up      = XYZ.BasisZ;
                    var right   = up.CrossProduct(viewDir);
                    t.BasisX = right;
                    t.BasisY = viewDir.CrossProduct(right);
                    t.BasisZ = viewDir;
                }

                double w = 3.0, h = 3.0;
                var elType = doc.GetElement(el.GetTypeId());
                double? bv = TryParam(elType, new[]{"b","Width","Beam Width","Diameter","D"});
                double? hv = TryParam(elType, new[]{"h","Height","Beam Height","Depth","Diameter","D"});
                if (bv.HasValue) w = bv.Value * 4;
                if (hv.HasValue) h = hv.Value * 4;

                var bbox       = new BoundingBoxXYZ { Transform = t };
                bbox.Min       = new XYZ(-w/2, -h/2, -1.0);
                bbox.Max       = new XYZ( w/2,  h/2,  1.0);

                var vs = ViewSection.CreateSection(doc, vt.Id, bbox);
                try { vs.Name = name; } catch { vs.Name = $"{name}_{el.Id}"; }
                return vs;
            }
            catch { return null; }
        }

        private static void CreateSectionDims(Document doc, ViewSection view, Element el)
        {
            try
            {
                var opt = new Options { ComputeReferences = true, View = view };
                var geom = el.get_Geometry(opt);
                Solid solid = null;
                if (geom != null) foreach (var g in geom)
                {
                    if (g is Solid s && s.Volume > 0) { solid = s; break; }
                    if (g is GeometryInstance gi) foreach (var gi2 in gi.GetInstanceGeometry())
                        if (gi2 is Solid s2 && s2.Volume > 0) { solid = s2; break; }
                }
                if (solid == null) return;

                var vRight = view.RightDirection; var vUp = view.UpDirection; var vOrg = view.Origin;
                var fVert = new List<PlanarFace>(); var fHorz = new List<PlanarFace>();
                double minU=1e9,maxU=-1e9,minV=1e9,maxV=-1e9;

                foreach (Face f in solid.Faces)
                {
                    if (f is not PlanarFace pf) continue;
                    var n = pf.FaceNormal;
                    if (Math.Abs(n.DotProduct(vRight)) > 0.9)
                    {
                        fVert.Add(pf);
                        double u = (pf.Origin - vOrg).DotProduct(vRight);
                        if (u < minU) minU=u; if (u > maxU) maxU=u;
                    }
                    else if (Math.Abs(n.DotProduct(vUp)) > 0.9)
                    {
                        fHorz.Add(pf);
                        double v = (pf.Origin - vOrg).DotProduct(vUp);
                        if (v < minV) minV=v; if (v > maxV) maxV=v;
                    }
                }
                double off = 50.0/304.8;
                if (fVert.Count >= 2)
                {
                    fVert.Sort((a, b) => (a.Origin-vOrg).DotProduct(vRight)
                                        .CompareTo((b.Origin-vOrg).DotProduct(vRight)));
                    var ra = new ReferenceArray();
                    ra.Append(fVert[0].Reference); ra.Append(fVert[fVert.Count-1].Reference);
                    double lv = maxV + off;
                    doc.Create.NewDimension(view,
                        Autodesk.Revit.DB.Line.CreateBound(vOrg+vRight*minU+vUp*lv, vOrg+vRight*maxU+vUp*lv), ra);
                }
                if (fHorz.Count >= 2)
                {
                    fHorz.Sort((a, b) => (a.Origin-vOrg).DotProduct(vUp)
                                        .CompareTo((b.Origin-vOrg).DotProduct(vUp)));
                    var ra = new ReferenceArray();
                    ra.Append(fHorz[0].Reference); ra.Append(fHorz[fHorz.Count-1].Reference);
                    double lu = minU - off;
                    doc.Create.NewDimension(view,
                        Autodesk.Revit.DB.Line.CreateBound(vOrg+vRight*lu+vUp*minV, vOrg+vRight*lu+vUp*maxV), ra);
                }
            }
            catch { /* dim errors are non-fatal */ }
        }

        // ── Private utilities ────────────────────────────────────────────
        private static double? TryParam(Element? el, string[] names)
        {
            if (el == null) return null;
            foreach (var n in names)
            {
                var p = el.LookupParameter(n);
                if (p != null && p.HasValue) return p.AsDouble();
            }
            return null;
        }

        private static void AddRect(Canvas canvas, double l, double t, double w, double h,
                                    Brush stroke, double thickness, Brush fill)
        {
            var r = new System.Windows.Shapes.Rectangle
                { Width = w, Height = h, Stroke = stroke, StrokeThickness = thickness,
                  Fill = fill ?? Brushes.Transparent };
            Canvas.SetLeft(r, l); Canvas.SetTop(r, t);
            canvas.Children.Add(r);
        }

        private static void AddCircle(Canvas canvas, double cx, double cy, double diaDraw, Brush fill)
        {
            double d = Math.Max(diaDraw, 3.0);
            var e = new WpfEllipse { Width = d, Height = d, Fill = fill };
            Canvas.SetLeft(e, cx - d/2); Canvas.SetTop(e, cy - d/2);
            canvas.Children.Add(e);
        }

        private static void SetCb(ComboBox cb, string? val)
        {
            if (val == null) return;
            int i = cb.Items.IndexOf(val);
            if (i >= 0) cb.SelectedIndex = i;
        }

        private static Dictionary<string, int> ParseZone(JObject? obj) => new()
        {
            ["T1"] = ProfileManager.GetVal(obj, "T1", 2),
            ["T2"] = ProfileManager.GetVal(obj, "T2", 0),
            ["B1"] = ProfileManager.GetVal(obj, "B1", 2),
            ["B2"] = ProfileManager.GetVal(obj, "B2", 0)
        };

        private static JObject ZoneToJObject(Dictionary<string, int> z) =>
            new() { ["T1"]=z["T1"], ["T2"]=z["T2"], ["B1"]=z["B1"], ["B2"]=z["B2"] };

        private static void Try(Action a) { try { a(); } catch { } }
        private static void Msg(string m)  => MessageBox.Show(m, "Rebar Generator");
        private static bool Confirm(string m) =>
            MessageBox.Show(m, "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

        private string AskString(string prompt, string title, string def = "")
        {
            var dlg = new InputDialog(prompt, title, def) { Owner = this };
            return dlg.ShowDialog() == true ? dlg.Value : string.Empty;
        }
        // ── Public property accessors (read by RebarGeneratorCommand after dialog closes) ──
        // Beam
        public string TopBarTypeSelected    => top_bar_type_selector.SelectedItem as string ?? "";
        public string BotBarTypeSelected    => bot_bar_type_selector.SelectedItem as string ?? "";
        public string StirrupSelector       => stirrup_selector.SelectedItem as string ?? "";
        public string TopL2TypeSelected     => top_L2_type.SelectedItem as string ?? "";
        public string BotL2TypeSelected     => bot_L2_type.SelectedItem as string ?? "";
        public string EndSpacingText        => end_spacing.Text;
        public string MidSpacingText        => mid_spacing.Text;
        public string SideCoverText         => side_cover.Text;
        public bool   ContinuityChecked     => chk_continuity.IsChecked == true;
        public bool   ChkTopSame            => chk_top_same.IsChecked == true;
        public bool   ChkBotSame            => chk_bot_same.IsChecked == true;
        public string SpacerTypeSelected    => spacer_type.SelectedItem as string ?? "None";
        public string SpacerSpacingText     => spacer_spacing.Text;
        public string BeamSideTypeSelected  => beam_side_type.SelectedItem as string ?? "";
        public string BeamSideQtySelected   => beam_side_qty.SelectedItem as string ?? "0";

        // Column
        public string ColWidthText          => col_width_ui.Text;
        public string ColDepthText          => col_depth_ui.Text;
        public string ColCoverText          => col_cover.Text;
        public string ColTieSpacingEndText  => col_tie_spacing_end.Text;
        public string ColTieSpacingMidText  => col_tie_spacing_mid.Text;
        public string ColConfHeightText     => col_conf_height.Text;
        public string ColTopExtensionText   => col_top_extension.Text;
        public string ColTieTypeSelected    => col_tie_type.SelectedItem as string ?? "";
        public string ColCornerTypeSelected => col_corner_type.SelectedItem as string ?? "";
        public string ColSideXTypeSelected  => col_side_x_type.SelectedItem as string ?? "";
        public string ColSideYTypeSelected  => col_side_y_type.SelectedItem as string ?? "";
        public string ColSideXQtySelected   => col_side_x_qty.SelectedItem as string ?? "0";
        public string ColSideYQtySelected   => col_side_y_qty.SelectedItem as string ?? "0";
        public bool   ColAnchorageChecked   => chk_col_anchorage.IsChecked == true;
        public bool   ColSpliceChecked      => chk_col_splice.IsChecked == true;
        public string ColSpliceLapText      => col_splice_lap.Text;
        public bool   ColCrankChecked       => chk_col_crank.IsChecked == true;
        public bool   ColInternalTiesChecked=> chk_col_internal_ties.IsChecked == true;

        // Footing
        public string FootingCoverText        => footing_cover.Text;
        public bool   FootingTopMatChecked    => chk_footing_top_mat.IsChecked == true;
        public string FootingBxTypeSelected   => footing_bx_type.SelectedItem as string ?? "";
        public string FootingBxSpacingText    => footing_bx_spacing.Text;
        public string FootingByTypeSelected   => footing_by_type.SelectedItem as string ?? "";
        public string FootingBySpacingText    => footing_by_spacing.Text;
        public string FootingTxTypeSelected   => footing_tx_type.SelectedItem as string ?? "";
        public string FootingTxSpacingText    => footing_tx_spacing.Text;
        public string FootingTyTypeSelected   => footing_ty_type.SelectedItem as string ?? "";
        public string FootingTySpacingText    => footing_ty_spacing.Text;
        public bool   FootingDowelChecked     => chk_footing_dowels.IsChecked == true;
        public string FootingDowelTypeSelected=> footing_dowel_type.SelectedItem as string ?? "";
        public string FootingDowelLapText     => footing_dowel_lap.Text;
        public string FootingDowelQxSelected  => footing_dowel_qx.SelectedItem as string ?? "2";
        public string FootingDowelQySelected  => footing_dowel_qy.SelectedItem as string ?? "2";
    }
}
