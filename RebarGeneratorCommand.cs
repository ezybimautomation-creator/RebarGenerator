using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ToolsByGimhan.RebarGenerator.Helpers;
using ToolsByGimhan.RebarGenerator.Models;

namespace ToolsByGimhan.RebarGenerator;

[Transaction(TransactionMode.Manual)]
public sealed class RebarGeneratorCommand : IExternalCommand
{
	private static readonly BuiltInCategory CAT_FRAMING = BuiltInCategory.OST_StructuralFraming;

	private static readonly BuiltInCategory CAT_COLUMNS = BuiltInCategory.OST_StructuralColumns;

	private static readonly BuiltInCategory CAT_FOOTINGS = BuiltInCategory.OST_StructuralFoundation;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIApplication application = commandData.Application;
		UIDocument activeUIDocument = application.ActiveUIDocument;
		Document doc = activeUIDocument.Document;
		try
		{
			using (Transaction transaction = new Transaction(doc, "Ensure Rebar Bar Types"))
			{
				transaction.Start();
				TryEnsureBarType(doc, "6mm", 6.0);
				TryEnsureBarType(doc, "8mm", 8.0);
				TryEnsureBarType(doc, "10mm", 10.0);
				TryEnsureBarType(doc, "12mm", 12.0);
				transaction.Commit();
			}
			List<RebarBarType> bts = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().ToList();
			List<(string, double)> list = (from b in bts
				select (name: GetBarName(b), diam: GetBarDiam(b)) into d
				where !string.IsNullOrEmpty(d.name)
				orderby d.name
				select d).ToList();
			if (list.Count == 0)
			{
				TaskDialog.Show("Rebar Generator", "No Rebar Bar Types found in the project.");
				return Result.Cancelled;
			}
			string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
			ProfileManager pm = new ProfileManager(assemblyDirectory);
			RebarGeneratorWindow rebarGeneratorWindow = new RebarGeneratorWindow(activeUIDocument);
			rebarGeneratorWindow.SetupData(list, list, pm);
			if (rebarGeneratorWindow.ShowDialog() != true)
			{
				return Result.Succeeded;   // Not .Cancelled ? committed work (e.g. sections) must persist
			}
			int selectedTabIndex = rebarGeneratorWindow.SelectedTabIndex;
			if (selectedTabIndex == 3)
			{
				return Result.Succeeded;
			}
			if (1 == 0)
			{
			}
			BuiltInCategory builtInCategory = selectedTabIndex switch
			{
				0 => CAT_FRAMING, 
				1 => CAT_COLUMNS, 
				2 => CAT_FOOTINGS, 
				_ => CAT_FRAMING, 
			};
			if (1 == 0)
			{
			}
			BuiltInCategory targetCategory = builtInCategory;
			CategorySelectionFilter selectionFilter = new CategorySelectionFilter(targetCategory);
			if (1 == 0)
			{
			}
			string text = selectedTabIndex switch
			{
				0 => "Select Beams", 
				1 => "Select Columns", 
				2 => "Select Footings", 
				_ => "Select Elements", 
			};
			if (1 == 0)
			{
			}
			string statusPrompt = text;
			IList<Reference> source;
			try
			{
				source = activeUIDocument.Selection.PickObjects(ObjectType.Element, selectionFilter, statusPrompt);
			}
			catch
			{
				return Result.Cancelled;
			}
			List<Element> list2 = source.Select((Reference r) => doc.GetElement(r)).ToList();
			if (list2.Count == 0)
			{
				TaskDialog.Show("Rebar Generator", "Nothing selected.");
				return Result.Cancelled;
			}
			RebarBarType rebarBarType = FindT((rebarGeneratorWindow.StirrupTypesData.Count > 0) ? rebarGeneratorWindow.StirrupSelector : null);
			RebarBarType rebarBarType2 = FindT(rebarGeneratorWindow.TopBarTypeSelected);
			RebarBarType rebarBarType3 = FindT(rebarGeneratorWindow.BotBarTypeSelected);
			if (rebarBarType == null || rebarBarType2 == null || rebarBarType3 == null)
			{
				TaskDialog.Show("Rebar Generator", "One or more selected Rebar Types not found.");
				return Result.Cancelled;
			}
			double.TryParse(rebarGeneratorWindow.EndSpacingText, out var result);
			double es = result / 304.8;
			double.TryParse(rebarGeneratorWindow.MidSpacingText, out var result2);
			double ms = result2 / 304.8;
			double.TryParse(rebarGeneratorWindow.SideCoverText, out var result3);
			double num = result3 / 304.8;
			double off = num;
			string text2 = rebarGeneratorWindow.SpacerTypeSelected ?? "None";
			RebarBarType spT = ((text2 != "None") ? FindT(text2) : null);
			double.TryParse(rebarGeneratorWindow.SpacerSpacingText, out var result4);
			double num2 = result4 / 304.8;
			if (num2 <= 0.0)
			{
				num2 = 3.2808398950131235;
			}
			RebarBarType sideT = FindT(rebarGeneratorWindow.BeamSideTypeSelected);
			int result5;
			int sideQ = (int.TryParse(rebarGeneratorWindow.BeamSideQtySelected, out result5) ? result5 : 0);
			RebarHookType hookT = GetHookByAngle(135) ?? GetHookByAngle(90);
			List<string> debugLog = new List<string>();
			switch (selectedTabIndex)
			{
			case 0:
			{
				List<Element> list4 = list2.Where((Element e) => ElementIdHelper.IsCategory(e, CAT_FRAMING)).ToList();
				if (list4.Count == 0)
				{
					TaskDialog.Show("Rebar Generator", "No Beams in selection.");
					return Result.Cancelled;
				}
				CreateBeamBatched(doc, activeUIDocument, rebarGeneratorWindow, list4, rebarBarType, rebarBarType2, rebarBarType3, hookT, GetHook, FindT, num, off, es, ms, spT, num2, sideT, sideQ, Log);
				break;
			}
			case 1:
			{
				List<Element> list5 = list2.Where((Element e) => ElementIdHelper.IsCategory(e, CAT_COLUMNS)).ToList();
				if (list5.Count == 0)
				{
					TaskDialog.Show("Rebar Generator", "No Columns in selection.");
					return Result.Cancelled;
				}
				RebarHookType colHookT = GetHookByAngle(90) ?? hookT;  // 90? hook for shorter inward hooks
				CreateColumnBatched(doc, activeUIDocument, rebarGeneratorWindow, list5, colHookT, GetHook, FindT, Log);
				break;
			}
			case 2:
			{
				List<Element> list3 = list2.Where((Element e) => ElementIdHelper.IsCategory(e, CAT_FOOTINGS)).ToList();
				if (list3.Count == 0)
				{
					TaskDialog.Show("Rebar Generator", "No Footings in selection.");
					return Result.Cancelled;
				}
				CreateFootingBatched(doc, activeUIDocument, rebarGeneratorWindow, list3, GetHook, FindT, Log);
				break;
			}
			}
			if (debugLog.Count > 0)
			{
				TaskDialog.Show("Rebar Generator", "Log:\n" + string.Join("\n", debugLog));
			}
			return Result.Succeeded;
			RebarBarType FindT(string name)
			{
				string n = name ?? "";
				return bts.FirstOrDefault((RebarBarType b) => GetBarName(b) == n);
			}
			void Log(string m)
			{
				debugLog.Add(m);
			}
		}
		catch (Exception ex)
		{
			message = ex.ToString();
			return Result.Failed;
		}
		RebarHookType GetHook()
		{
			return GetHookByAngle(90);
		}
		RebarHookType GetHookByAngle(int angle)
		{
			IEnumerable<RebarHookType> source2 = new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>();
			return source2.FirstOrDefault((RebarHookType h) => GetHookName(h).Contains(angle.ToString())) ?? source2.FirstOrDefault();
		}
	}

	private void CreateBeamBatched(Document doc, UIDocument uidoc, RebarGeneratorWindow win, List<Element> beams, RebarBarType stT, RebarBarType topBtT, RebarBarType botBtT, RebarHookType hookT, Func<RebarHookType> getHook90, Func<string, RebarBarType> findT, double cov, double off, double es, double ms, RebarBarType spT, double spS, RebarBarType sideT, int sideQ, Action<string> log)
	{
		View view;
		using (Transaction transaction = new Transaction(doc, "Batch Beam Rebar"))
		{
			transaction.Start();
			view = doc.ActiveView;
			bool flag = view is View3D;
			double num = DiamOf(stT);
			double num2 = DiamOf(topBtT);
			double num3 = (win.ChkTopSame ? num2 : DiamOf(findT(win.TopL2TypeSelected) ?? topBtT));
			double num4 = DiamOf(botBtT);
			double num5 = (win.ChkBotSame ? num4 : DiamOf(findT(win.BotL2TypeSelected) ?? botBtT));
			foreach (Element beam in beams)
			{
				Element b = beam;
				log($"Processing Beam: {b.Id}");
				var (bw, bh, cy, cz) = GetBeamDimsGeometric(b);
				if (bw <= 0.0)
				{
					continue;
				}
				((Element el, string type) start, (Element el, string type) end) supportsAtEnds = GetSupportsAtEnds(doc, b);
				(Element, string) item = supportsAtEnds.start;
				(Element, string) item2 = supportsAtEnds.end;
				Element s0 = item.Item1;
				string t0 = item.Item2;
				Element s1 = item2.Item1;
				string t1 = item2.Item2;
				Curve curve = (b.Location as LocationCurve)?.Curve;
				if (curve == null)
				{
					continue;
				}
				XYZ endPoint = curve.GetEndPoint(0);
				XYZ endPoint2 = curve.GetEndPoint(1);
				if (!(b is FamilyInstance familyInstance))
				{
					continue;
				}
				Transform trans = familyInstance.GetTransform();
				XYZ bx = trans.BasisX;
				XYZ basisZ = trans.BasisZ;
				XYZ by = trans.BasisY;
				double lx0 = 0.0;
				double lx1 = 0.0;
				try
				{
					GetBeamSolidExtents(b, trans, bx, endPoint, endPoint2, out lx0, out lx1);
				}
				catch
				{
					lx0 = (endPoint - trans.Origin).DotProduct(bx);
					lx1 = (endPoint2 - trans.Origin).DotProduct(bx);
				}
				if (lx0 > lx1)
				{
					double num6 = lx0;
					lx0 = lx1;
					lx1 = num6;
				}
				double num7 = (lx1 - lx0) / 3.0;
				bool continuityChecked = win.ContinuityChecked;
				Dictionary<string, int> dictionary = win.ZoneConfigs["End Sections"];
				Dictionary<string, int> dictionary2 = win.ZoneConfigs["Middle Section"];
				bool flag2 = continuityChecked && dictionary.GetValueOrDefault("T1") == dictionary2.GetValueOrDefault("T1") && dictionary.GetValueOrDefault("T1") > 0;
				bool flag3 = continuityChecked && dictionary.GetValueOrDefault("B1") == dictionary2.GetValueOrDefault("B1") && dictionary.GetValueOrDefault("B1") > 0;
				bool flag4 = continuityChecked && dictionary.GetValueOrDefault("T2") == dictionary2.GetValueOrDefault("T2") && dictionary.GetValueOrDefault("T2") > 0;
				bool flag5 = continuityChecked && dictionary.GetValueOrDefault("B2") == dictionary2.GetValueOrDefault("B2") && dictionary.GetValueOrDefault("B2") > 0;
				if (flag2)
				{
					double yLocal = bh / 2.0 - cov - num - num2 / 2.0;
					double ew = bw - 2.0 * cov - 2.0 * num - num2;
					double sx = (0.0 - bw) / 2.0 + cov + num + num2 / 2.0;
					DrawCont(yLocal, dictionary["T1"], ew, sx, topBtT, isTop: true);
				}
				if (flag3)
				{
					double yLocal2 = (0.0 - bh) / 2.0 + cov + num + num4 / 2.0;
					double ew2 = bw - 2.0 * cov - 2.0 * num - num4;
					double sx2 = (0.0 - bw) / 2.0 + cov + num + num4 / 2.0;
					DrawCont(yLocal2, dictionary["B1"], ew2, sx2, botBtT, isTop: false);
				}
				if (flag4)
				{
					double num8 = Math.Max(Math.Max(num2, num3), 0.08202099737532809);
					double yLocal3 = bh / 2.0 - cov - num - num2 / 2.0 - num2 / 2.0 - num8 - num3 / 2.0;
					double ew3 = bw - 2.0 * cov - 2.0 * num - num3;
					double sx3 = (0.0 - bw) / 2.0 + cov + num + num3 / 2.0;
					DrawCont(yLocal3, dictionary["T2"], ew3, sx3, findT(win.TopL2TypeSelected) ?? topBtT, isTop: true);
				}
				if (flag5)
				{
					double num9 = Math.Max(Math.Max(num4, num5), 0.08202099737532809);
					double yLocal4 = (0.0 - bh) / 2.0 + cov + num + num4 / 2.0 + num4 / 2.0 + num9 + num5 / 2.0;
					double ew4 = bw - 2.0 * cov - 2.0 * num - num5;
					double sx4 = (0.0 - bw) / 2.0 + cov + num + num5 / 2.0;
					DrawCont(yLocal4, dictionary["B2"], ew4, sx4, findT(win.BotL2TypeSelected) ?? botBtT, isTop: false);
				}
				MakeStir(lx0 + off, lx0 + num7, es);
				MakeStir(lx0 + num7, lx0 + 2.0 * num7, ms);
				MakeStir(lx0 + 2.0 * num7, lx1 - off, es);
				double num10 = lx0 + GetAnchorage(s0, isStart: true);
				double num11 = lx1 - GetAnchorage(s1, isStart: false);
				double num12 = num11 - num10;
				if (num12 <= 0.0)
				{
					num12 = lx1 - lx0;
				}
				double num13 = num12 / 3.0;
				double num14 = num12 * 0.1;
				(string, double, double, int)[] array = new(string, double, double, int)[3]
				{
					("End Sections", lx0 + off, lx0 + num7, 0),
					("Middle Section", lx0 + num7, lx0 + 2.0 * num7, 1),
					("End Sections", lx0 + 2.0 * num7, lx1 - off, 2)
				};
				(string, double, double, int)[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					var (zName, num15, num16, num17) = array2[i];
					if (num16 <= num15)
					{
						continue;
					}
					var (list, list2) = GetRebarSets(win, zName, bw, bh, cov, num, num2, num3, num4, num5, spT, spS, flag2, flag3, flag4, flag5);
					foreach (RebarSetDef rb in list)
					{
						RebarHookType rebarHookType = null;
						RebarHookType rebarHookType2 = null;
						string layerTag = rb.LayerTag;
						bool flag6 = ((layerTag == "T1" || layerTag == "T2") ? true : false);
						bool flag7 = flag6;
						double num18 = num15;
						double num19 = num16;
						if (layerTag == "T2" && !flag4)
						{
							switch (num17)
							{
							case 0:
								num18 = lx0;
								num19 = num10 + num13;
								break;
							case 2:
								num18 = num11 - num13;
								num19 = lx1;
								break;
							}
						}
						else if (layerTag == "B2" && !flag5 && num17 == 1)
						{
							num18 = num10 + num14;
							num19 = num11 - num14;
						}
						if (num17 == 0 && t0 == "Column")
						{
							rebarHookType = getHook90();
						}
						if (num17 == 2 && t1 == "Column")
						{
							rebarHookType2 = getHook90();
						}
						if (rebarHookType != null)
						{
							num18 -= GetAnchorage(s0, isStart: true);
						}
						if (rebarHookType2 != null)
						{
							num19 += GetAnchorage(s1, isStart: false);
						}
						if (!(num19 <= num18))
						{
							int qty3 = rb.Qty;
							double[] array3 = ((qty3 == 1) ? new double[1] : (from num36 in Enumerable.Range(0, qty3)
								select rb.StartXLocal + (double)num36 * (rb.ArrayLen / (double)(qty3 - 1))).ToArray());
							RebarHookOrientation hOrient = (flag7 ? RebarHookOrientation.Left : RebarHookOrientation.Right);
							RebarBarType bt = findT(rb.TypeName) ?? topBtT;
							double[] array4 = array3;
							foreach (double yOff in array4)
							{
								MkBar(num18, num19, yOff, rb.YLocal, bt, rebarHookType, rebarHookType2, hOrient);
							}
						}
					}
					foreach (var (num21, barType, num22) in list2)
					{
						try
						{
							double num23 = bw - 2.0 * cov - 2.0 * num;
							if (num23 <= 0.0)
							{
								continue;
							}
							List<Curve> curves = new List<Curve> { Line.CreateBound(trans.OfPoint(new XYZ(num15, (0.0 - num23) / 2.0 + cy, num21 + cz)), trans.OfPoint(new XYZ(num15, num23 / 2.0 + cy, num21 + cz))) };
							Rebar rebar = Rebar.CreateFromCurves(doc, RebarStyle.Standard, barType, null, null, b, bx, curves, RebarHookOrientation.Right, RebarHookOrientation.Right, useExistingShapeIfPossible: true, createNewShape: true);
							if (rebar != null)
							{
								double num24 = num16 - num15;
								int num25 = (int)Math.Floor(num24 / num22);
								if (num25 > 1)
								{
									rebar.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(num25, num22, barsOnNormalSide: true, includeFirstBar: true, includeLastBar: true);
								}
								SetVis(rebar);
							}
						}
						catch
						{
						}
					}
				}
				if (sideT == null || sideQ <= 0)
				{
					continue;
				}
				try
				{
					double num26 = DiamOf(sideT);
					double num27 = bh - 2.0 * cov - 2.0 * num;
					int num28 = sideQ / 2;
					if (!(num27 > 0.0) || num28 <= 0)
					{
						continue;
					}
					double num29 = num27 / (double)(num28 + 1);
					double num30 = (0.0 - num27) / 2.0 + num29;
					double num31 = bw / 2.0 - cov - num - num26 / 2.0;
					for (int num32 = 0; num32 < num28; num32++)
					{
						double num33 = num30 + (double)num32 * num29;
						double[] array5 = new double[2]
						{
							0.0 - num31,
							num31
						};
						foreach (double num35 in array5)
						{
							List<Curve> curves2 = new List<Curve> { Line.CreateBound(trans.OfPoint(new XYZ(lx0 + off, num35 + cy, num33 + cz)), trans.OfPoint(new XYZ(lx1 - off, num35 + cy, num33 + cz))) };
							Rebar r = Rebar.CreateFromCurves(doc, RebarStyle.Standard, sideT, null, null, b, basisZ, curves2, RebarHookOrientation.Right, RebarHookOrientation.Right, useExistingShapeIfPossible: true, createNewShape: true);
							SetVis(r);
						}
					}
				}
				catch (Exception ex)
				{
					log("SideBar Err: " + ex.Message);
				}
				void DrawCont(double zOff, int num36, double num38, double num39, RebarBarType bt2, bool isTop)
				{
					double xloc = lx0 + off;
					RebarHookType hS = null;
					double xloc2 = lx1 - off;
					RebarHookType hE = null;
					if (t0 == "Column")
					{
						xloc = lx0 - GetAnchorage(s0, isStart: true);
						hS = getHook90();
					}
					if (t1 == "Column")
					{
						xloc2 = lx1 + GetAnchorage(s1, isStart: false);
						hE = getHook90();
					}
					RebarHookOrientation hOrient2 = (isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right);
					double[] array6 = ((num36 == 1) ? new double[1] : (from num40 in Enumerable.Range(0, num36)
						select num39 + (double)num40 * (num38 / (double)(num36 - 1))).ToArray());
					double[] array7 = array6;
					foreach (double yOff2 in array7)
					{
						MkBar(xloc, xloc2, yOff2, zOff, bt2, hS, hE, hOrient2);
					}
				}
				double GetAnchorage(Element suppEl, bool isStart)
				{
					try
					{
						if (suppEl != null && ElementIdHelper.IsCategory(suppEl, CAT_COLUMNS))
						{
							double colDepthAlongBeam = GetColDepthAlongBeam(suppEl, trans, isStart ? lx0 : lx1);
							return Math.Max(colDepthAlongBeam - cov, 125.0 / 381.0);
						}
					}
					catch
					{
					}
					return 0.49212598425196846;
				}
				void MakeStir(double num38, double num40, double sp)
				{
					double num36 = bw - 2.0 * cov;
					double num37 = bh - 2.0 * cov;
					XYZ[] array6 = new XYZ[4]
					{
						new XYZ(num38, cy + num36 / 2.0, cz + num37 / 2.0),
						new XYZ(num38, cy - num36 / 2.0, cz + num37 / 2.0),
						new XYZ(num38, cy - num36 / 2.0, cz - num37 / 2.0),
						new XYZ(num38, cy + num36 / 2.0, cz - num37 / 2.0)
					};
					List<Curve> list3 = new List<Curve>();
					for (int j = 0; j < 4; j++)
					{
						list3.Add(Line.CreateBound(trans.OfPoint(array6[j]), trans.OfPoint(array6[(j + 1) % 4])));
					}
					try
					{
						Rebar rebar2 = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, stT, null, null, b, bx, list3, RebarHookOrientation.Left, RebarHookOrientation.Left, useExistingShapeIfPossible: true, createNewShape: true);
						if (rebar2 != null)
						{
							if (hookT != null)
							{
								try { rebar2.SetHookTypeId(0, hookT.Id); } catch { }
								try { rebar2.SetHookTypeId(1, hookT.Id); } catch { }
							}
							double num39 = num40 - num38;
							int num41 = (int)Math.Floor(num39 / sp) + 1;
							if (num41 > 1)
							{
								rebar2.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(num41, sp, barsOnNormalSide: true, includeFirstBar: true, includeLastBar: true);
							}
							SetVis(rebar2);
						}
					}
					catch (Exception ex2)
					{
						log("Stirrup Err: " + ex2.Message);
					}
				}
				void MkBar(double xloc0, double xloc1, double num36, double zOff, RebarBarType barType2, RebarHookType hS, RebarHookType hE, RebarHookOrientation rebarHookOrientation)
				{
					try
					{
						XYZ xYZ = trans.OfPoint(new XYZ(xloc0, num36 + cy, zOff + cz));
						XYZ xYZ2 = trans.OfPoint(new XYZ(xloc1, num36 + cy, zOff + cz));
						if (!(xYZ.DistanceTo(xYZ2) < 0.001))
						{
							List<Curve> curves3 = new List<Curve> { Line.CreateBound(xYZ, xYZ2) };
							Rebar r2 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, barType2, hS, hE, b, by, curves3, rebarHookOrientation, rebarHookOrientation, useExistingShapeIfPossible: true, createNewShape: true);
							SetVis(r2);
						}
					}
					catch
					{
					}
				}
			}
			transaction.Commit();
			TaskDialog.Show("Rebar Generator", "Beam Rebar Complete!");
		}
		static double DiamOf(RebarBarType rebarBarType)
		{
			return ((Element)rebarBarType).get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
		}
		void SetVis(Element element)
		{
			try
			{
				if (element is Rebar rebar2)
				{
					rebar2.SetUnobscuredInView(view, unobscured: true);
				}
			}
			catch
			{
			}
		}
	}

	private void CreateColumnBatched(Document doc, UIDocument uidoc, RebarGeneratorWindow win, List<Element> cols, RebarHookType hookT, Func<RebarHookType> getHook90, Func<string, RebarBarType> findT, Action<string> log)
		{
			double.TryParse(win.ColWidthText, out var result);
			double num = result / 304.8;
			double.TryParse(win.ColDepthText, out var result2);
			double num2 = result2 / 304.8;
			double.TryParse(win.ColCoverText, out var result3);
			double ccov = result3 / 304.8;
			double.TryParse(win.ColTieSpacingEndText, out var result4);
			double item = result4 / 304.8;
			double.TryParse(win.ColTieSpacingMidText, out var result5);
			double item2 = result5 / 304.8;
			double.TryParse(win.ColConfHeightText, out var result6);
			double.TryParse(win.ColTopExtensionText, out var result7);
			double extLen = result7 / 304.8;
			RebarBarType rebarBarType = findT(win.ColTieTypeSelected);
			RebarBarType rebarBarType2 = findT(win.ColCornerTypeSelected);
			RebarBarType rebarBarType3 = findT(win.ColSideXTypeSelected);
			RebarBarType rebarBarType4 = findT(win.ColSideYTypeSelected);
			if (rebarBarType == null || rebarBarType2 == null)
			{
				TaskDialog.Show("Rebar Generator", "Missing Bar Types for column.");
				return;
			}
			double num3 = ((Element)rebarBarType).get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
			double num4 = ((Element)rebarBarType2).get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
			int result8;
			int num5 = (int.TryParse(win.ColSideXQtySelected, out result8) ? result8 : 0);
			int result9;
			int num6 = (int.TryParse(win.ColSideYQtySelected, out result9) ? result9 : 0);
			View view2;
			using (Transaction transaction = new Transaction(doc, "Batch Column Rebar"))
			{
				transaction.Start();
				view2 = doc.ActiveView;
				bool flag = view2 is View3D;
				foreach (Element col2 in cols)
				{
					Element col = col2;
					log($"Processing Column: {col.Id}");
					BoundingBoxXYZ boundingBoxXYZ = col.get_BoundingBox((View)null);
					if (boundingBoxXYZ == null || !(col is FamilyInstance familyInstance))
					{
						continue;
					}
					Autodesk.Revit.DB.Transform transform = familyInstance.GetTransform();
					XYZ org = transform.Origin;
					double zMin = boundingBoxXYZ.Min.Z;
					double zMax = boundingBoxXYZ.Max.Z;
					double minZGeo;
					double maxZGeo;
					if (GetColumnDimsGeometric(col).bd > 0.0)
					{
						Options options = new Options
						{
							DetailLevel = ViewDetailLevel.Fine
						};
						GeometryElement enumerable = col.get_Geometry(options);
						minZGeo = 99999.0;
						maxZGeo = -99999.0;
						Traverse(enumerable);
						if (minZGeo < 99998.0)
						{
							zMin = minZGeo;
							zMax = maxZGeo;
						}
					}
					double num7 = zMax - zMin;
					XYZ bx = transform.BasisX;
					XYZ by2 = transform.BasisY;
					XYZ basisZ = transform.BasisZ;
					double anchDepth = 0.0;
					RebarHookType hookBot = null;
					if (win.ColAnchorageChecked)
					{
						Element element = FindFoundationBelow(doc, col, boundingBoxXYZ);
						if (element != null)
						{
							BoundingBoxXYZ boundingBoxXYZ2 = element.get_BoundingBox((View)null);
							if (boundingBoxXYZ2 != null)
							{
								double num8 = zMin - boundingBoxXYZ2.Min.Z - ccov;
								if (num8 > 0.0)
								{
									anchDepth = num8;
									hookBot = getHook90();
								}
							}
						}
					}
					double num9 = ((result6 > 0.0) ? (result6 / 304.8) : (num7 / 6.0));
					double item3 = zMin - anchDepth + ccov;
					(double, double, double)[] array = new(double, double, double)[3]
					{
						(item3, zMin + num9, item),
						(zMin + num9, zMax - num9, item2),
						(zMax - num9, zMax - ccov, item)
					};
					double num10 = num / 2.0 - ccov - num3 / 2.0;
					double num11 = num2 / 2.0 - ccov - num3 / 2.0;
					double num12 = num / 2.0 - ccov - num3 - num4 / 2.0;
					double num13 = num2 / 2.0 - ccov - num3 - num4 / 2.0;
					XYZ[] array2 = new XYZ[4]
					{
						org + bx * (0.0 - num10) + by2 * num11,
						org + bx * (0.0 - num10) + by2 * (0.0 - num11),
						org + bx * num10 + by2 * (0.0 - num11),
						org + bx * num10 + by2 * num11
					};
					(double, double, double)[] array3 = array;
					for (int i = 0; i < array3.Length; i++)
					{
						var (num14, num15, num16) = array3[i];
						if (num15 <= num14)
						{
							continue;
						}
						List<Curve> curves = new List<Curve>
						{
							Autodesk.Revit.DB.Line.CreateBound(PZ(array2[0], num14), PZ(array2[1], num14)),
							Autodesk.Revit.DB.Line.CreateBound(PZ(array2[1], num14), PZ(array2[2], num14)),
							Autodesk.Revit.DB.Line.CreateBound(PZ(array2[2], num14), PZ(array2[3], num14)),
							Autodesk.Revit.DB.Line.CreateBound(PZ(array2[3], num14), PZ(array2[0], num14))
						};
						try
						{
							Rebar rebar = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, rebarBarType, null, null, col, basisZ, curves, RebarHookOrientation.Left, RebarHookOrientation.Left, useExistingShapeIfPossible: true, createNewShape: true);
							if (rebar != null)
							{
								if (hookT != null)
								{
									try { rebar.SetHookTypeId(0, hookT.Id); } catch { }
									try { rebar.SetHookTypeId(1, hookT.Id); } catch { }
								}
								int num17 = (int)((num15 - num14) / num16) + 1;
								if (num17 > 1)
								{
									rebar.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(num17, num16, barsOnNormalSide: true, includeFirstBar: true, includeLastBar: true);
								}
								SetVis(rebar);
							}
						}
						catch (Exception ex)
						{
							log("Tie Err: " + ex.Message);
						}
						if (win.CustomColTies.Count <= 0)
						{
							continue;
						}
						List<(double, double)> list = new List<(double, double)>();
						list.Add((0.0 - num12, num13));
						list.Add((num12, num13));
						list.Add((num12, 0.0 - num13));
						list.Add((0.0 - num12, 0.0 - num13));
						if (num5 > 0)
						{
							double num18 = 2.0 * num12 / (double)(num5 + 1);
							for (int j = 1; j <= num5; j++)
								list.Add((0.0 - num12 + (double)j * num18, num13)); // Top Face
							for (int j = 1; j <= num5; j++)
								list.Add((0.0 - num12 + (double)j * num18, 0.0 - num13)); // Bot Face
						}
						if (num6 > 0)
						{
							double num19 = 2.0 * num13 / (double)(num6 + 1);
							for (int k = 1; k <= num6; k++)
								list.Add((0.0 - num12, num13 - (double)k * num19)); // Left Face
							for (int k = 1; k <= num6; k++)
								list.Add((num12, num13 - (double)k * num19)); // Right Face
						}
						
						var allColTies = new List<List<int>>(win.CustomColTies);
						if (win.ColInternalTiesChecked) allColTies.AddRange(win.GetAutoTies());

						foreach (List<int> customColTy in allColTies)
						{
							if (customColTy.Count < 3)
							{
								continue;
							}
							List<(double, double)> list2 = new List<(double, double)>();
							foreach (int item6 in customColTy)
							{
								if (item6 < list.Count)
								{
									list2.Add(list[item6]);
								}
							}
							if (list2.Count < 3)
							{
								continue;
							}
							List<(double, double)> list3 = new List<(double, double)>();
							foreach (var item7 in list2)
							{
								bool flag2 = false;
								foreach (var item8 in list3)
								{
									if (Math.Abs(item8.Item1 - item7.Item1) < 1E-06 && Math.Abs(item8.Item2 - item7.Item2) < 1E-06)
									{
										flag2 = true;
										break;
									}
								}
								if (!flag2)
								{
									list3.Add(item7);
								}
							}
							if (list3.Count < 3)
							{
								continue;
							}
							double cx0 = 0.0;
							double cy0 = 0.0;
							foreach (var (num20, num21) in list3)
							{
								cx0 += num20;
								cy0 += num21;
							}
							cx0 /= list3.Count;
							cy0 /= list3.Count;
							list3.Sort(((double x, double y) a, (double x, double y) b) => Math.Atan2(b.y - cy0, b.x - cx0).CompareTo(Math.Atan2(a.y - cy0, a.x - cx0)));
							List<XYZ> list4 = new List<XYZ>();
							foreach (var (num22, num23) in list3)
							{
								list4.Add(org + bx * num22 + by2 * num23);
							}
							List<Curve> list5 = new List<Curve>();
							for (int num24 = 0; num24 < list4.Count; num24++)
							{
								XYZ xYZ = PZ(list4[num24], num14);
								XYZ xYZ2 = PZ(list4[(num24 + 1) % list4.Count], num14);
								if (!(xYZ.DistanceTo(xYZ2) < 1E-06))
								{
									list5.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ, xYZ2));
								}
							}
							if (list5.Count < 3)
							{
								continue;
							}
							try
							{
								Rebar rebar2 = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, rebarBarType, null, null, col, basisZ, list5, RebarHookOrientation.Right, RebarHookOrientation.Right, useExistingShapeIfPossible: true, createNewShape: true);
								if (rebar2 != null)
								{
									if (hookT != null)
									{
										try { rebar2.SetHookTypeId(0, hookT.Id); } catch { }
										try { rebar2.SetHookTypeId(1, hookT.Id); } catch { }
									}
									int num25 = (int)((num15 - num14) / num16) + 1;
									if (num25 > 1)
									{
										rebar2.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(num25, num16, barsOnNormalSide: true, includeFirstBar: true, includeLastBar: true);
									}
									SetVis(rebar2);
								}
							}
							catch (Exception ex2)
							{
								log("CustomTie Err: " + ex2.Message);
							}
						}
					}
					bool doSplice = win.ColSpliceChecked;
					bool doCrank = win.ColCrankChecked;
					double result10;
					double lapF = (double.TryParse(win.ColSpliceLapText, out result10) ? result10 : 40.0);
					(double, double)[] array4 = new(double, double)[4]
					{
						(0.0 - num12, 0.0 - num13),
						(num12, 0.0 - num13),
						(num12, num13),
						(0.0 - num12, num13)
					};
					for (int num26 = 0; num26 < array4.Length; num26++)
					{
						var (num27, num28) = array4[num26];
						MkVert(num27, num28, rebarBarType2, num4);
					}
					if (num5 > 0 && rebarBarType3 != null)
					{
						double num29 = ((Element)rebarBarType3).get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
						double num30 = 2.0 * num12 / (double)(num5 + 1);
						for (int num31 = 1; num31 <= num5; num31++)
						{
							double num32 = 0.0 - num12 + (double)num31 * num30;
							int[] array5 = new int[2] { -1, 1 };
							int[] array6 = array5;
							foreach (int num34 in array6)
							{
								XYZ source = new XYZ(0.0, num34, 0.0);
								XYZ xYZ3 = XYZ.BasisZ.CrossProduct(source);
								XYZ norm = bx * xYZ3.X + by2 * xYZ3.Y;
								double num35 = num2 / 2.0 - ccov - num3 - num29 / 2.0;
								XYZ xYZ4 = bx * num32 + by2 * (num35 * (double)num34);
								double num36 = ((win.ColTopExtensionText != "0") ? extLen : 0.0);
								double num37 = (doSplice ? (lapF * num29) : num36);
								XYZ xYZ5 = new XYZ(org.X + xYZ4.X, org.Y + xYZ4.Y, zMin + ccov - anchDepth);
								XYZ endpoint = new XYZ(xYZ5.X, xYZ5.Y, zMax - ccov + num37);
								List<Curve> list6 = new List<Curve>();
								if (doSplice && doCrank && zMax - zMin > num37)
								{
									double num38 = Math.Max(10.0 * num29, 250.0 / 381.0);
									double num39 = num29 * 1.5;
									XYZ xYZ6 = -by2 * num34;
									XYZ xYZ7 = new XYZ(xYZ5.X, xYZ5.Y, zMax - ccov - num38);
									XYZ xYZ8 = new XYZ(xYZ7.X + xYZ6.X * num39, xYZ7.Y + xYZ6.Y * num39, zMax - ccov);
									XYZ endpoint2 = new XYZ(xYZ8.X, xYZ8.Y, zMax - ccov + num37);
									list6.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ5, xYZ7));
									list6.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ7, xYZ8));
									list6.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ8, endpoint2));
								}
								else
								{
									list6.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ5, endpoint));
								}
								try
								{
									Rebar element2 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, rebarBarType3, hookBot, null, col, norm, list6, RebarHookOrientation.Left, RebarHookOrientation.Left, useExistingShapeIfPossible: false, createNewShape: true);
									SetVis(element2);
								}
								catch
								{
								}
							}
						}
					}
					if (num6 <= 0 || rebarBarType4 == null)
					{
						continue;
					}
					double num40 = 2.0 * num13 / (double)(num6 + 1);
					for (int num41 = 1; num41 <= num6; num41++)
					{
						double num42 = 0.0 - num13 + (double)num41 * num40;
						int[] array7 = new int[2] { -1, 1 };
						int[] array8 = array7;
						foreach (int num44 in array8)
						{
							double num45 = ((Element)rebarBarType4).get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
							XYZ source2 = new XYZ(num44, 0.0, 0.0);
							XYZ xYZ9 = XYZ.BasisZ.CrossProduct(source2);
							XYZ norm2 = bx * xYZ9.X + by2 * xYZ9.Y;
							double num46 = num / 2.0 - ccov - num3 - num45 / 2.0;
							XYZ xYZ10 = bx * (num46 * (double)num44) + by2 * num42;
							double num47 = ((win.ColTopExtensionText != "0") ? extLen : 0.0);
							double num48 = (doSplice ? (lapF * num45) : num47);
							XYZ xYZ11 = new XYZ(org.X + xYZ10.X, org.Y + xYZ10.Y, zMin + ccov - anchDepth);
							XYZ endpoint3 = new XYZ(xYZ11.X, xYZ11.Y, zMax - ccov + num48);
							List<Curve> list7 = new List<Curve>();
							if (doSplice && doCrank && zMax - zMin > num48)
							{
								double num49 = Math.Max(10.0 * num45, 250.0 / 381.0);
								double num50 = num45 * 1.5;
								XYZ xYZ12 = -bx * num44;
								XYZ xYZ13 = new XYZ(xYZ11.X, xYZ11.Y, zMax - ccov - num49);
								XYZ xYZ14 = new XYZ(xYZ13.X + xYZ12.X * num50, xYZ13.Y + xYZ12.Y * num50, zMax - ccov);
								XYZ endpoint4 = new XYZ(xYZ14.X, xYZ14.Y, zMax - ccov + num48);
								list7.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ11, xYZ13));
								list7.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ13, xYZ14));
								list7.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ14, endpoint4));
							}
							else
							{
								list7.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ11, endpoint3));
							}
							try
							{
								Rebar element3 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, rebarBarType4, hookBot, null, col, norm2, list7, RebarHookOrientation.Left, RebarHookOrientation.Left, useExistingShapeIfPossible: false, createNewShape: true);
								SetVis(element3);
							}
							catch
							{
							}
						}
					}
					void MkVert(double num53, double num54, RebarBarType bt, double barD)
					{
						double num51 = ((win.ColTopExtensionText != "0") ? extLen : 0.0);
						double num52 = (doSplice ? (lapF * barD) : num51);
						XYZ xYZ15 = bx * num53 + by2 * num54;
						XYZ xYZ16 = new XYZ(org.X + xYZ15.X, org.Y + xYZ15.Y, zMin + ccov - anchDepth);
						XYZ endpoint5 = new XYZ(xYZ16.X, xYZ16.Y, zMax - ccov + num52);
						List<Curve> list8 = new List<Curve>();
						if (doSplice && doCrank && zMax - zMin > num52)
						{
							double num55 = Math.Max(10.0 * barD, 250.0 / 381.0);
							double num56 = barD * 1.5;
							XYZ xYZ17 = -(bx * num53 + by2 * num54).Normalize();
							XYZ xYZ18 = new XYZ(xYZ16.X, xYZ16.Y, zMax - ccov - num55);
							XYZ xYZ19 = new XYZ(xYZ18.X + xYZ17.X * num56, xYZ18.Y + xYZ17.Y * num56, zMax - ccov);
							XYZ endpoint6 = new XYZ(xYZ19.X, xYZ19.Y, zMax - ccov + num52);
							list8.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ16, xYZ18));
							list8.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ18, xYZ19));
							list8.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ19, endpoint6));
						}
						else
						{
							list8.Add(Autodesk.Revit.DB.Line.CreateBound(xYZ16, endpoint5));
						}
						try
						{
							XYZ source3 = new XYZ(num53, num54, 0.0).Normalize();
							XYZ xYZ20 = XYZ.BasisZ.CrossProduct(source3);
							XYZ norm3 = bx * xYZ20.X + by2 * xYZ20.Y;
							Rebar element4 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, bt, hookBot, null, col, norm3, list8, RebarHookOrientation.Left, RebarHookOrientation.Left, useExistingShapeIfPossible: true, createNewShape: true);
							SetVis(element4);
						}
						catch
						{
						}
					}
					void Traverse(IEnumerable<GeometryObject> enumerable2)
					{
						foreach (GeometryObject item9 in enumerable2)
						{
							if (item9 is Solid { Volume: >0.0 } solid)
							{
								foreach (Edge edge in solid.Edges)
								{
									foreach (XYZ item10 in edge.Tessellate())
									{
										if (item10.Z < minZGeo)
										{
											minZGeo = item10.Z;
										}
										if (item10.Z > maxZGeo)
										{
											maxZGeo = item10.Z;
										}
									}
								}
							}
							else if (item9 is GeometryInstance geometryInstance)
							{
								Traverse(geometryInstance.GetInstanceGeometry());
							}
						}
					}
				}
				transaction.Commit();
				TaskDialog.Show("Rebar Generator", "Column Rebar Created!");
			}
		static XYZ PZ(XYZ pb, double zv)
		{
			return new XYZ(pb.X, pb.Y, zv);
		}
		void SetVis(Element element2)
		{
			try
			{
				if (element2 is Rebar rebar2)
				{
					rebar2.SetUnobscuredInView(view2, unobscured: true);
				}
			}
			catch
			{
			}
		}
	}

	private void CreateFootingBatched(Document doc, UIDocument uidoc, RebarGeneratorWindow win, List<Element> footings, Func<RebarHookType> getHook90, Func<string, RebarBarType> findT, Action<string> log)
	{
		View viewF;
		using (Transaction transaction = new Transaction(doc, "Batch Footing Rebar"))
		{
			transaction.Start();
			viewF = doc.ActiveView;
			bool flag = viewF is View3D;
			foreach (Element footing in footings)
			{
				Element f = footing;
				try
				{
					if (!(f is FamilyInstance familyInstance))
					{
						continue;
					}
					Transform trans = familyInstance.GetTransform();
					Transform inv = trans.Inverse;
					Options options = new Options
					{
						DetailLevel = ViewDetailLevel.Fine
					};
					GeometryElement g = f.get_Geometry(options);
					List<Solid> solids = new List<Solid>();
					GetSols(g);
					if (solids.Count == 0)
					{
						continue;
					}
					Solid solid = solids.OrderByDescending((Solid s) => s.Volume).First();
					double lMinX = 9999.0;
					double lMaxX = -9999.0;
					double lMinY = 9999.0;
					double lMaxY = -9999.0;
					double lMinZ = 9999.0;
					double lMaxZ = -9999.0;
					foreach (Edge edge in solid.Edges)
					{
						foreach (XYZ item in edge.Tessellate())
						{
							XYZ xYZ = inv.OfPoint(item);
							if (xYZ.X < lMinX)
							{
								lMinX = xYZ.X;
							}
							if (xYZ.X > lMaxX)
							{
								lMaxX = xYZ.X;
							}
							if (xYZ.Y < lMinY)
							{
								lMinY = xYZ.Y;
							}
							if (xYZ.Y > lMaxY)
							{
								lMaxY = xYZ.Y;
							}
							if (xYZ.Z < lMinZ)
							{
								lMinZ = xYZ.Z;
							}
							if (xYZ.Z > lMaxZ)
							{
								lMaxZ = xYZ.Z;
							}
						}
					}
					double fw = Math.Abs(lMaxX - lMinX);
					double fl2 = Math.Abs(lMaxY - lMinY);
					double.TryParse(win.FootingCoverText, out var result);
					double fcov = result / 304.8;
					RebarHookType h90 = getHook90();
					CreateMat(isTop: false);
					if (win.FootingTopMatChecked)
					{
						CreateMat(isTop: true);
					}
					if (win.FootingDowelChecked)
					{
						CreateDowels();
					}
					void CreateDowels()
					{
						RebarBarType tDwl = findT(win.FootingDowelTypeSelected);
						double zBot;
						double zTop;
						double leg;
						if (tDwl != null)
						{
							int result2;
							int num = (int.TryParse(win.FootingDowelQxSelected, out result2) ? result2 : 2);
							int result3;
							int num2 = (int.TryParse(win.FootingDowelQySelected, out result3) ? result3 : 2);
							double result4;
							double num3 = (double.TryParse(win.FootingDowelLapText, out result4) ? result4 : 40.0);
							double barModelDiameter = tDwl.BarModelDiameter;
							double num4 = num3 * barModelDiameter;
							BoundingBoxXYZ boundingBoxXYZ = f.get_BoundingBox((View)null);
							XYZ xYZ2 = ((boundingBoxXYZ != null) ? ((boundingBoxXYZ.Min + boundingBoxXYZ.Max) / 2.0) : trans.Origin);
							double num5 = boundingBoxXYZ?.Max.Z ?? trans.Origin.Z;
							double num6 = 1.3123359580052494;
							double num7 = 1.3123359580052494;
							Outline outline = new Outline(new XYZ(xYZ2.X - 1.5, xYZ2.Y - 1.5, num5 - 0.5), new XYZ(xYZ2.X + 1.5, xYZ2.Y + 1.5, num5 + 2.0));
							IList<Element> list = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WherePasses(new BoundingBoxIntersectsFilter(outline)).ToElements();
							double x = inv.OfPoint(xYZ2).X;
							double y = inv.OfPoint(xYZ2).Y;
							if (list.Count > 0)
							{
								Element element = list.First();
								(double, double, double, double) columnDimsGeometric = GetColumnDimsGeometric(element);
								if (columnDimsGeometric.Item1 > 0.1 && columnDimsGeometric.Item2 > 0.1)
								{
									(num6, num7, _, _) = columnDimsGeometric;
								}
								BoundingBoxXYZ boundingBoxXYZ2 = element.get_BoundingBox((View)null);
								if (boundingBoxXYZ2 != null)
								{
									XYZ point = (boundingBoxXYZ2.Min + boundingBoxXYZ2.Max) / 2.0;
									XYZ xYZ3 = inv.OfPoint(point);
									x = xYZ3.X;
									y = xYZ3.Y;
								}
							}
							zBot = lMinZ + fcov;
							zTop = lMaxZ + num4;
							leg = Math.Min(num6, num7) * 0.75;
							double num8 = 50.0 / 381.0;
							double num9 = num6 - 2.0 * num8;
							double num10 = num7 - 2.0 * num8;
							if (num9 < 0.1)
							{
								num9 = num6 * 0.5;
							}
							if (num10 < 0.1)
							{
								num10 = num7 * 0.5;
							}
							if (num > 0)
							{
								double num11 = ((num > 1) ? (num9 / (double)(num - 1)) : 0.0);
								double num12 = x - num9 / 2.0;
								for (int i = 0; i < num; i++)
								{
									double x2 = num12 + (double)i * num11;
									DrawDwl(new XYZ(x2, y + num10 / 2.0, 0.0), new XYZ(0.0, 1.0, 0.0), XYZ.BasisX);
									DrawDwl(new XYZ(x2, y - num10 / 2.0, 0.0), new XYZ(0.0, -1.0, 0.0), XYZ.BasisX);
								}
							}
							int num13 = ((num > 0) ? 1 : 0);
							int num14 = ((num > 0) ? (num2 - 1) : num2);
							if (num2 > 0)
							{
								double num15 = ((num2 > 1) ? (num10 / (double)(num2 - 1)) : 0.0);
								double num16 = y - num10 / 2.0;
								for (int j = num13; j < num14; j++)
								{
									double y2 = num16 + (double)j * num15;
									DrawDwl(new XYZ(x + num9 / 2.0, y2, 0.0), new XYZ(1.0, 0.0, 0.0), XYZ.BasisY);
									DrawDwl(new XYZ(x - num9 / 2.0, y2, 0.0), new XYZ(-1.0, 0.0, 0.0), XYZ.BasisY);
								}
							}
						}
						void DrawDwl(XYZ pBase, XYZ dirOut, XYZ norm)
						{
							XYZ point2 = new XYZ(pBase.X + dirOut.X * leg, pBase.Y + dirOut.Y * leg, zBot);
							XYZ point3 = new XYZ(pBase.X, pBase.Y, zBot);
							XYZ point4 = new XYZ(pBase.X, pBase.Y, zTop);
							List<Curve> curves = new List<Curve>
							{
								Line.CreateBound(trans.OfPoint(point2), trans.OfPoint(point3)),
								Line.CreateBound(trans.OfPoint(point3), trans.OfPoint(point4))
							};
							try
							{
								Rebar r = Rebar.CreateFromCurves(doc, RebarStyle.Standard, tDwl, null, null, f, trans.OfVector(norm), curves, RebarHookOrientation.Left, RebarHookOrientation.Left, useExistingShapeIfPossible: true, createNewShape: true);
								SetVis(r);
							}
							catch
							{
							}
						}
					}
					void CreateMat(bool isTop)
					{
						double z = (isTop ? (lMaxZ - fcov) : (lMinZ + fcov));
						string arg = (isTop ? win.FootingTxTypeSelected : win.FootingBxTypeSelected);
						double.TryParse(isTop ? win.FootingTxSpacingText : win.FootingBxSpacingText, out var result2);
						double num = result2 / 304.8;
						RebarBarType rebarBarType = findT(arg);
						if (rebarBarType != null)
						{
							XYZ point = new XYZ(lMinX + fcov, lMinY + fcov, z);
							XYZ point2 = new XYZ(lMaxX - fcov, lMinY + fcov, z);
							List<Curve> curves = new List<Curve> { Line.CreateBound(trans.OfPoint(point), trans.OfPoint(point2)) };
							XYZ basisY = trans.BasisY;
							RebarHookOrientation rebarHookOrientation = (isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right);
							Rebar rebar = Rebar.CreateFromCurves(doc, RebarStyle.Standard, rebarBarType, h90, h90, f, basisY, curves, rebarHookOrientation, rebarHookOrientation, useExistingShapeIfPossible: true, createNewShape: true);
							if (rebar != null)
							{
								int numberOfBarPositions = (int)((fl2 - 2.0 * fcov) / num) + 1;
								rebar.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(numberOfBarPositions, num, barsOnNormalSide: true, includeFirstBar: true, includeLastBar: true);
								SetVis(rebar);
							}
						}
						string arg2 = (isTop ? win.FootingTyTypeSelected : win.FootingByTypeSelected);
						double.TryParse(isTop ? win.FootingTySpacingText : win.FootingBySpacingText, out var result3);
						double num2 = result3 / 304.8;
						RebarBarType rebarBarType2 = findT(arg2);
						if (rebarBarType2 != null)
						{
							XYZ point3 = new XYZ(lMinX + fcov, lMinY + fcov, z);
							XYZ point4 = new XYZ(lMinX + fcov, lMaxY - fcov, z);
							List<Curve> curves2 = new List<Curve> { Line.CreateBound(trans.OfPoint(point3), trans.OfPoint(point4)) };
							XYZ basisX = trans.BasisX;
							RebarHookOrientation rebarHookOrientation2 = ((!isTop) ? RebarHookOrientation.Left : RebarHookOrientation.Right);
							Rebar rebar2 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, rebarBarType2, h90, h90, f, basisX, curves2, rebarHookOrientation2, rebarHookOrientation2, useExistingShapeIfPossible: true, createNewShape: true);
							if (rebar2 != null)
							{
								int numberOfBarPositions2 = (int)((fw - 2.0 * fcov) / num2) + 1;
								rebar2.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(numberOfBarPositions2, num2, barsOnNormalSide: true, includeFirstBar: true, includeLastBar: true);
								SetVis(rebar2);
							}
						}
					}
					void GetSols(IEnumerable<GeometryObject> enumerable)
					{
						foreach (GeometryObject item2 in enumerable)
						{
							if (item2 is Solid { Volume: >0.0 } solid2)
							{
								solids.Add(solid2);
							}
							else if (item2 is GeometryInstance geometryInstance)
							{
								GetSols(geometryInstance.GetInstanceGeometry());
							}
						}
					}
				}
				catch (Exception ex)
				{
					log("Footing Err: " + ex.Message);
				}
			}
			transaction.Commit();
			TaskDialog.Show("Rebar Generator", "Footing Rebar Created!");
		}
		void SetVis(Element r)
		{
			try
			{
				if (r is Rebar rebar)
				{
					rebar.SetUnobscuredInView(viewF, unobscured: true);
				}
			}
			catch
			{
			}
		}
	}

	private static (double bw, double bd, double cx, double cy) GetColumnDimsGeometric(Element b)
	{
		double num = 0.0;
		double num2 = 0.0;
		double item = 0.0;
		double item2 = 0.0;
		FamilySymbol e = (b as FamilyInstance)?.Symbol;
		string[] names = new string[6] { "b", "Width", "Column Width", "BF", "B", "Width (b)" };
		string[] names2 = new string[6] { "h", "Depth", "Column Depth", "D", "Height", "H" };
		num = TryP(e, names) ?? TryP(b, names).GetValueOrDefault();
		num2 = TryP(e, names2) ?? TryP(b, names2).GetValueOrDefault();
		try
		{
			Options options = new Options
			{
				ComputeReferences = true,
				DetailLevel = ViewDetailLevel.Fine
			};
			GeometryElement g = b.get_Geometry(options);
			List<Solid> solids = new List<Solid>();
			GetSols(g);
			if (solids.Count > 0)
			{
				Solid solid = solids.OrderByDescending((Solid s) => s.Volume).First();
				XYZ point = solid.ComputeCentroid();
				if (!(b is FamilyInstance familyInstance))
				{
					return (bw: num, bd: num2, cx: item, cy: item2);
				}
				Transform transform = familyInstance.GetTransform();
				Transform inverse = transform.Inverse;
				XYZ xYZ = inverse.OfPoint(point);
				item = xYZ.X;
				item2 = xYZ.Y;
				if (num <= 0.0 || num2 <= 0.0)
				{
					double num3 = 99999.0;
					double num4 = -99999.0;
					double num5 = 99999.0;
					double num6 = -99999.0;
					foreach (Edge edge in solid.Edges)
					{
						foreach (XYZ item3 in edge.Tessellate())
						{
							XYZ xYZ2 = inverse.OfPoint(item3);
							if (xYZ2.X < num3)
							{
								num3 = xYZ2.X;
							}
							if (xYZ2.X > num4)
							{
								num4 = xYZ2.X;
							}
							if (xYZ2.Y < num5)
							{
								num5 = xYZ2.Y;
							}
							if (xYZ2.Y > num6)
							{
								num6 = xYZ2.Y;
							}
						}
					}
					if (num <= 0.0)
					{
						num = num4 - num3;
					}
					if (num2 <= 0.0)
					{
						num2 = num6 - num5;
					}
				}
			}
			void GetSols(IEnumerable<GeometryObject> enumerable)
			{
				foreach (GeometryObject item4 in enumerable)
				{
					if (item4 is Solid { Volume: >0.0 } solid2)
					{
						solids.Add(solid2);
					}
					else if (item4 is GeometryInstance geometryInstance)
					{
						GetSols(geometryInstance.GetInstanceGeometry());
					}
				}
			}
		}
		catch
		{
		}
		return (bw: num, bd: num2, cx: item, cy: item2);
		static double? TryP(Element? element, string[] array)
		{
			if (element == null)
			{
				return null;
			}
			foreach (string name in array)
			{
				Parameter parameter = element.LookupParameter(name);
				if (parameter != null && parameter.HasValue)
				{
					return parameter.AsDouble();
				}
			}
			return null;
		}
	}

	private static (double bw, double bh, double cy, double cz) GetBeamDimsGeometric(Element b)
	{
		double num = 0.0;
		double num2 = 0.0;
		double item = 0.0;
		double item2 = 0.0;
		FamilySymbol e = (b as FamilyInstance)?.Symbol;
		string[] names = new string[6] { "b", "Width", "Beam Width", "BF", "B", "Width (b)" };
		string[] names2 = new string[7] { "h", "Height", "Beam Height", "D", "Depth", "H", "Height (h)" };
		num = TryP(e, names) ?? TryP(b, names).GetValueOrDefault();
		num2 = TryP(e, names2) ?? TryP(b, names2).GetValueOrDefault();
		try
		{
			Options options = new Options
			{
				ComputeReferences = true,
				DetailLevel = ViewDetailLevel.Fine
			};
			GeometryElement g = b.get_Geometry(options);
			List<Solid> solids = new List<Solid>();
			GetSols(g);
			if (solids.Count > 0)
			{
				Solid solid = solids.OrderByDescending((Solid s) => s.Volume).First();
				if (!(b is FamilyInstance familyInstance))
				{
					return (bw: num, bh: num2, cy: item, cz: item2);
				}
			Transform transform = familyInstance.GetTransform();
				Transform inverse = transform.Inverse;

				// cy = 0 always: rectangular beams are symmetric about Y in local coords
				// Only compute cz (Z offset) since beam reference line position varies
				item = 0.0;

				double num5 = 99999.0, num6 = -99999.0;
				double num3 = 99999.0, num4 = -99999.0;
				foreach (Edge edge in solid.Edges)
				{
					foreach (XYZ pt in edge.Tessellate())
					{
						XYZ lp = inverse.OfPoint(pt);
						if (lp.Z < num5) num5 = lp.Z;
						if (lp.Z > num6) num6 = lp.Z;
						if (lp.Y < num3) num3 = lp.Y;
						if (lp.Y > num4) num4 = lp.Y;
					}
				}

				item2 = (num5 + num6) / 2.0;  // cz = Z midpoint

				if (num <= 0.0) num   = num4 - num3;
				if (num2 <= 0.0) num2 = num6 - num5;
			}
			void GetSols(IEnumerable<GeometryObject> enumerable)
			{
				foreach (GeometryObject item4 in enumerable)
				{
					if (item4 is Solid { Volume: >0.0 } solid2)
					{
						solids.Add(solid2);
					}
					else if (item4 is GeometryInstance geometryInstance)
					{
						GetSols(geometryInstance.GetInstanceGeometry());
					}
				}
			}
		}
		catch
		{
		}
		return (bw: num, bh: num2, cy: item, cz: item2);
		static double? TryP(Element? element, string[] array)
		{
			if (element == null)
			{
				return null;
			}
			foreach (string name in array)
			{
				Parameter parameter = element.LookupParameter(name);
				if (parameter != null && parameter.HasValue)
				{
					return parameter.AsDouble();
				}
			}
			return null;
		}
	}

	private static void GetBeamSolidExtents(Element b, Transform trans, XYZ bx, XYZ p0, XYZ p1, out double lx0, out double lx1)
	{
		lx0 = 99999.0;
		lx1 = -99999.0;
		Options options = new Options
		{
			ComputeReferences = true,
			DetailLevel = ViewDetailLevel.Fine
		};
		GeometryElement g = b.get_Geometry(options);
		List<Solid> solids = new List<Solid>();
		GetSols(g);
		if (solids.Count == 0)
		{
			lx0 = (p0 - trans.Origin).DotProduct(bx);
			lx1 = (p1 - trans.Origin).DotProduct(bx);
			return;
		}
		Solid solid = solids.OrderByDescending((Solid s) => s.Volume).First();
		Transform inverse = trans.Inverse;
		foreach (Edge edge in solid.Edges)
		{
			foreach (XYZ item in edge.Tessellate())
			{
				double x = inverse.OfPoint(item).X;
				if (x < lx0)
				{
					lx0 = x;
				}
				if (x > lx1)
				{
					lx1 = x;
				}
			}
		}
		void GetSols(IEnumerable<GeometryObject> enumerable)
		{
			foreach (GeometryObject item2 in enumerable)
			{
				if (item2 is Solid { Volume: >0.0 } solid2)
				{
					solids.Add(solid2);
				}
				else if (item2 is GeometryInstance geometryInstance)
				{
					GetSols(geometryInstance.GetInstanceGeometry());
				}
			}
		}
	}

	private static ((Element el, string type) start, (Element el, string type) end) GetSupportsAtEnds(Document doc, Element beam)
	{
		Curve curve = (beam.Location as LocationCurve)?.Curve;
		if (curve == null)
		{
			return (start: (el: null, type: "None"), end: (el: null, type: "None"));
		}
		XYZ endPoint = curve.GetEndPoint(0);
		XYZ endPoint2 = curve.GetEndPoint(1);
		return (start: FindSupp(endPoint), end: FindSupp(endPoint2));
		(Element, string) FindSupp(XYZ pt)
		{
			Outline outline = new Outline(pt - new XYZ(0.5, 0.5, 0.5), pt + new XYZ(0.5, 0.5, 0.5));
			BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);
			ICollection<ElementId> collection = new FilteredElementCollector(doc).WherePasses(filter).ToElementIds();
			foreach (ElementId item in collection)
			{
				if (!(item == beam.Id))
				{
					Element element = doc.GetElement(item);
					if (element?.Category != null)
					{
						if (ElementIdHelper.IsCategory(element, BuiltInCategory.OST_StructuralColumns))
						{
							return (element, "Column");
						}
						if (ElementIdHelper.IsCategory(element, BuiltInCategory.OST_StructuralFraming))
						{
							return (element, "Beam");
						}
					}
				}
			}
			return (null, "None");
		}
	}

	private static double GetColDepthAlongBeam(Element col, Transform beamTrans, double lxBase)
	{
		try
		{
			Options options = new Options
			{
				ComputeReferences = true,
				DetailLevel = ViewDetailLevel.Fine
			};
			GeometryElement g = col.get_Geometry(options);
			List<Solid> solids = new List<Solid>();
			GetSols(g);
			if (solids.Count != 0)
			{
				Transform inverse = beamTrans.Inverse;
				double num = 99999.0;
				double num2 = -99999.0;
				bool flag = false;
				foreach (Edge edge in solids.OrderByDescending((Solid s) => s.Volume).First().Edges)
				{
					foreach (XYZ item in edge.Tessellate())
					{
						double x = inverse.OfPoint(item).X;
						if (x < num)
						{
							num = x;
						}
						if (x > num2)
						{
							num2 = x;
						}
						flag = true;
					}
				}
				if (flag)
				{
					double num3 = num2 - num;
					if (num3 > 0.3)
					{
						return num3;
					}
				}
			}
			BoundingBoxXYZ boundingBoxXYZ = col.get_BoundingBox((View)null);
			if (boundingBoxXYZ != null)
			{
				XYZ[] array = new XYZ[8]
				{
					boundingBoxXYZ.Min,
					boundingBoxXYZ.Max,
					new XYZ(boundingBoxXYZ.Min.X, boundingBoxXYZ.Min.Y, boundingBoxXYZ.Max.Z),
					new XYZ(boundingBoxXYZ.Min.X, boundingBoxXYZ.Max.Y, boundingBoxXYZ.Min.Z),
					new XYZ(boundingBoxXYZ.Max.X, boundingBoxXYZ.Min.Y, boundingBoxXYZ.Min.Z),
					new XYZ(boundingBoxXYZ.Max.X, boundingBoxXYZ.Max.Y, boundingBoxXYZ.Min.Z),
					new XYZ(boundingBoxXYZ.Max.X, boundingBoxXYZ.Min.Y, boundingBoxXYZ.Max.Z),
					new XYZ(boundingBoxXYZ.Min.X, boundingBoxXYZ.Max.Y, boundingBoxXYZ.Max.Z)
				};
				double num4 = 99999.0;
				double num5 = -99999.0;
				Transform inverse2 = beamTrans.Inverse;
				XYZ[] array2 = array;
				foreach (XYZ point in array2)
				{
					double x2 = inverse2.OfPoint(point).X;
					if (x2 < num4)
					{
						num4 = x2;
					}
					if (x2 > num5)
					{
						num5 = x2;
					}
				}
				return num5 - num4;
			}
			void GetSols(IEnumerable<GeometryObject> enumerable)
			{
				foreach (GeometryObject item2 in enumerable)
				{
					if (item2 is Solid { Volume: >0.0 } solid)
					{
						solids.Add(solid);
					}
					else if (item2 is GeometryInstance geometryInstance)
					{
						GetSols(geometryInstance.GetInstanceGeometry());
					}
				}
			}
		}
		catch
		{
		}
		return 0.5;
	}

	private static Element FindFoundationBelow(Document doc, Element col, BoundingBoxXYZ bb)
	{
		XYZ xYZ = (bb.Min + bb.Max) / 2.0;
		XYZ xYZ2 = new XYZ(xYZ.X, xYZ.Y, bb.Min.Z - 0.5);
		Outline outline = new Outline(xYZ2 - new XYZ(1.0, 1.0, 1.0), xYZ2 + new XYZ(1.0, 1.0, 1.0));
		BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);
		IList<Element> source = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation).WherePasses(filter).ToElements();
		return source.FirstOrDefault();
	}

	private static (List<RebarSetDef> sets, List<(double y, RebarBarType spType, double spSpacing)> spacers) GetRebarSets(RebarGeneratorWindow win, string zName, double bw, double bh, double cov, double sd, double mdT, double mdT2, double mdB, double mdB2, RebarBarType spT, double spS, bool skipT1, bool skipB1, bool skipT2, bool skipB2)
	{
		List<RebarSetDef> list = new List<RebarSetDef>();
		List<(double, RebarBarType, double)> list2 = new List<(double, RebarBarType, double)>();
		Dictionary<string, int> dictionary = win.ZoneConfigs.GetValueOrDefault(zName) ?? new Dictionary<string, int>();
		double arrayLen = bw - 2.0 * cov - 2.0 * sd - mdT;
		double startXLocal = (0.0 - bw) / 2.0 + cov + sd + mdT / 2.0;
		double arrayLen2 = bw - 2.0 * cov - 2.0 * sd - mdT2;
		double startXLocal2 = (0.0 - bw) / 2.0 + cov + sd + mdT2 / 2.0;
		double arrayLen3 = bw - 2.0 * cov - 2.0 * sd - mdB;
		double startXLocal3 = (0.0 - bw) / 2.0 + cov + sd + mdB / 2.0;
		double arrayLen4 = bw - 2.0 * cov - 2.0 * sd - mdB2;
		double startXLocal4 = (0.0 - bw) / 2.0 + cov + sd + mdB2 / 2.0;
		int valueOrDefault = dictionary.GetValueOrDefault("T1");
		if (valueOrDefault > 0 && !skipT1)
		{
			list.Add(new RebarSetDef(bh / 2.0 - cov - sd - mdT / 2.0, valueOrDefault, win.TopBarTypeSelected, mdT, arrayLen, startXLocal, "T1"));
		}
		valueOrDefault = dictionary.GetValueOrDefault("B1");
		if (valueOrDefault > 0 && !skipB1)
		{
			list.Add(new RebarSetDef((0.0 - bh) / 2.0 + cov + sd + mdB / 2.0, valueOrDefault, win.BotBarTypeSelected, mdB, arrayLen3, startXLocal3, "B1"));
		}
		valueOrDefault = dictionary.GetValueOrDefault("T2");
		if (valueOrDefault > 0)
		{
			double num = Math.Max(Math.Max(mdT, mdT2), 0.08202099737532809);
			double num2 = bh / 2.0 - cov - sd - mdT / 2.0 - mdT / 2.0 - num - mdT2 / 2.0;
			if (!skipT2)
			{
				list.Add(new RebarSetDef(num2, valueOrDefault, win.ChkTopSame ? win.TopBarTypeSelected : (win.TopL2TypeSelected ?? win.TopBarTypeSelected), mdT2, arrayLen2, startXLocal2, "T2"));
			}
			if (spT != null)
			{
				list2.Add((num2 + mdT2 / 2.0 + num / 2.0, spT, spS));
			}
		}
		valueOrDefault = dictionary.GetValueOrDefault("B2");
		if (valueOrDefault > 0)
		{
			double num3 = Math.Max(Math.Max(mdB, mdB2), 0.08202099737532809);
			double num4 = (0.0 - bh) / 2.0 + cov + sd + mdB / 2.0 + mdB / 2.0 + num3 + mdB2 / 2.0;
			if (!skipB2)
			{
				list.Add(new RebarSetDef(num4, valueOrDefault, win.ChkBotSame ? win.BotBarTypeSelected : (win.BotL2TypeSelected ?? win.BotBarTypeSelected), mdB2, arrayLen4, startXLocal4, "B2"));
			}
			if (spT != null)
			{
				list2.Add((num4 - mdB2 / 2.0 - num3 / 2.0, spT, spS));
			}
		}
		return (sets: list, spacers: list2);
	}

	private static string GetBarName(RebarBarType b)
	{
		return ((Element)b).get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString() ?? b.Name ?? string.Empty;
	}

	private static double GetBarDiam(RebarBarType b)
	{
		try
		{
			return ((Element)b).get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
		}
		catch
		{
			return 0.04;
		}
	}

	private static string GetHookName(RebarHookType h)
	{
		try
		{
			return ((Element)h).get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString() ?? h.Name ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static void TryEnsureBarType(Document doc, string name, double diaMm)
	{
		RebarBarType rebarBarType = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().FirstOrDefault((RebarBarType b) => GetBarName(b) == name || GetBarName(b) == name + "mm");
		if (rebarBarType == null)
		{
			List<RebarBarType> list = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().ToList();
			if (list.Count != 0 && list[0].Duplicate(name) is RebarBarType rebarBarType2)
			{
				((Element)rebarBarType2).get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).Set(diaMm / 304.8);
			}
		}
	}

}

