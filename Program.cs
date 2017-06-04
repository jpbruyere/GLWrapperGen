//
//  Program.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2016 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Xml;
using System.IO;
using Mono.CSharp;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace GLWrapperGen
{	

	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Opengl C# Wrapper Generator");

			if (Directory.Exists ("generated"))
				Directory.Delete ("generated", true);

			XmlDocument glspec = new XmlDocument();
			glspec.Load ("specs/gl.xml");

			buildGLTypeDic ();

			readTypes (glspec);
			readEnums(glspec);
			readCommands (glspec);

			writeConstants ();
			writeCSEnums2 ();
			writeCSICalls ();

			writeCICalls ();
		}

		public static string[] csReservedKeyword = new string [] {
			"ref",
			"object",
			"in",
			"out",
			"params",
			"string",
			"event",
			"base",
		};
		public static string[] extensions = new string[] {
			"EXT",
			"ARB",
			"NV",
			"NVX",
			"ATI",
			"3DLABS",
			"SUN",
			"SGI",
			"SGIX",
			"SGIS",
			"INTEL",
			"3DFX",
			"IBM",
			"MESA",
			"GREMEDY",
			"OML",
			"OES",
			"PGI",
			"I3D",
			"INGR",
			"MTX",
			"APPLE",
			"QCOM",
			"IMG",
			"KHR",
			"AMD"
		};

		public static string rootns = "Tetra";
		public static int tabsCountForValue = 8;
		public static int tabsCountForCst = 12;

		static Dictionary<string, string> CtoCSTypeDic = new Dictionary<string, string>();
		static List<glTypeDef> GLTypes = new List<glTypeDef> ();
		static Dictionary<string,List<string>> GLEnumGroups = new Dictionary<string, List<string>>();
		static List<glEnumDef> GLEnums = new List<glEnumDef> ();
		static List<glCommand> GLCommands =	new List<glCommand> ();

		static void readTypes(XmlDocument glspec){
			XmlNode xmlTypes = glspec.SelectSingleNode ("/registry/types");
			foreach (XmlNode xmlType in xmlTypes.SelectNodes("type")) {
				string typeNameAttrib = xmlType.Attributes ["name"]?.Value?.Trim();
				if (!string.IsNullOrEmpty (typeNameAttrib))
					continue;
				glTypeDef gtd = new glTypeDef ();
				gtd.name = xmlType.SelectSingleNode ("name")?.InnerXml?.Trim();
				if (string.IsNullOrEmpty (gtd.name))
					continue;
				gtd.requires = xmlType.Attributes ["requires"]?.Value?.Trim();
				gtd.api = xmlType.Attributes ["api"]?.Value;
				gtd.ctype = xmlType.FirstChild?.Value?.Substring(8)?.Trim();
				GLTypes.Add(gtd);
			}
		}
		static void readEnums(XmlDocument glspec){			
			XmlNode xmlGroups = glspec.SelectSingleNode("/registry/groups");
			foreach (XmlNode xmlGroup in xmlGroups.SelectNodes("group")) {
				string grpName = xmlGroup.Attributes ["name"].Value;
				List<string> enums = new List<string> ();
				foreach (XmlNode xmlGrpItem in xmlGroup.SelectNodes("enum")) {
					enums.Add (xmlGrpItem.Attributes ["name"].Value);	
				}
				GLEnumGroups [grpName] = enums;
			}

			XmlNodeList xmlEnums = glspec.SelectNodes("/registry/enums");
			foreach (XmlNode xmlEnum in xmlEnums) {
				glEnumDef def = new glEnumDef ();
				def.group = xmlEnum.Attributes ["group"]?.Value;
				def.ns = xmlEnum.Attributes ["namespace"]?.Value;
				def.vendor = xmlEnum.Attributes ["vendor"]?.Value;

				if (xmlEnum.Attributes ["type"]?.Value == "bitmask")
					def.bitmask = true;
				
				foreach (XmlNode xmlEnumValue in xmlEnum.SelectNodes("enum")) {					
					def.values.Add (new glEnumValue () {
						name = xmlEnumValue.Attributes ["name"]?.Value?.Trim(),
						api = xmlEnumValue.Attributes ["api"]?.Value?.Trim(),
						value = xmlEnumValue.Attributes ["value"]?.Value?.Trim() });
				}
				GLEnums.Add (def);
			}				
		}
		static void readCommands(XmlDocument glspec){
			XmlNodeList cmdsgroup = glspec.SelectNodes("/registry/commands");

			foreach (XmlNode xmlCmds in cmdsgroup) {
				string ns = xmlCmds.Attributes ["namespace"].Value;

				foreach (XmlNode xmlCmd in xmlCmds.SelectNodes("command")) {
					glCommand c = new glCommand ();
					XmlNode nProto = xmlCmd.SelectSingleNode ("proto");
					c.ns = ns;
					c.name = nProto.SelectSingleNode ("name").InnerXml;
					c.returnType = nProto.SelectSingleNode ("ptype")?.InnerXml;
					foreach (XmlNode xmlParam in xmlCmd.SelectNodes ("param")) {
						glParam glp = new glParam ();
						glp.type = xmlParam.SelectSingleNode ("ptype")?.InnerXml;
						glp.name = xmlParam.SelectSingleNode ("name")?.InnerXml;
						glp.group = xmlParam.Attributes ["group"]?.Value;
						c.paramList.Add (glp);
					}
					GLCommands.Add (c);
				}
			}
		}
		public const int cst = 10;

		static void writeConstants () {
			string outputFile = "constants.cs";
			foreach (IGrouping<string,glEnumDef> edg in GLEnums.GroupBy(e=>e.ns)) {
				string ns = edg.Key;
				string outputPath = string.Format (@"generated/{0}", ns);
				Directory.CreateDirectory (outputPath);
				using (TextWriter tw = new StreamWriter (Path.Combine (outputPath, outputFile))) {
					using (IndentedTextWriter itw = new IndentedTextWriter (tw)) {
						itw.WriteLine ("// Autogenerated File");
						itw.WriteLine ("using System;");
						itw.WriteLine ("namespace {0}", rootns);
						itw.WriteLine ("{");
						itw.Indent++;
						itw.WriteLine ("public static partial class {0}", ns);
						itw.WriteLine ("{");
						itw.Indent++;
						itw.WriteLine ("public static class All");
						itw.WriteLine ("{");
						itw.Indent++;

						foreach (glEnumDef edef in GLEnums.Where (gle=>string.IsNullOrEmpty(gle.group))) {
							foreach (glEnumValue eval in edef.values) {
								if (!string.IsNullOrEmpty (eval.api) && eval.api!="gl")
									continue;
								string cstName = ToCamelCase (eval.name);
								int tabs = (int)Math.Max (0, tabsCountForCst - Math.Floor ((double)(cstName.Length + 18) / 4.0));
								itw.WriteLine ("public const uint {0}{1}= {2};",
									cstName, new String('\t',tabs) ,eval.value);
							}
						}
						itw.Indent--;
						itw.WriteLine ("}");
						itw.Indent--;
						itw.WriteLine ("}");
						itw.Indent--;
						itw.WriteLine ("}");
					}
				}
			}
		}
		static void writeCSEnums2 () {
			string outputFile = "enums.cs";
			foreach (IGrouping<string,glEnumDef> edg in GLEnums.GroupBy(e=>e.ns)) {
				string ns = edg.Key;
				Console.WriteLine ("processing enums for {0}", ns);
				string outputPath = string.Format (@"generated/{0}", ns);
				Directory.CreateDirectory (outputPath);
				using (TextWriter tw = new StreamWriter (Path.Combine (outputPath, outputFile))) {
					using (IndentedTextWriter itw = new IndentedTextWriter (tw)) {
						itw.WriteLine ("// Autogenerated File");
						itw.WriteLine ("using System;");
						itw.WriteLine ("namespace {0}", rootns);
						itw.WriteLine ("{");
						itw.Indent++;
						itw.WriteLine ("public static partial class {0}", ns);
						itw.WriteLine ("{");
						itw.Indent++;
						foreach (IGrouping<string,string> groupedGrp in GLEnumGroups.Keys.GroupBy (k=>GetExtFromGroupName(k))) {
							string ext = groupedGrp.Key;
							if (!string.IsNullOrEmpty (ext)) {
								itw.WriteLine ("public static partial class {0}", ext);
								itw.WriteLine ("{");
								itw.Indent++;
							}

							foreach (string egrp in groupedGrp) {
								glEnumDef edef = GLEnums.Where (e => e.group == egrp).FirstOrDefault ();
								if (edef != null) {
									if (edef.bitmask)
										itw.WriteLine ("[Flags]");
								}
								string enumName = egrp;
								if (!string.IsNullOrEmpty (ext))
									enumName = enumName.Remove (enumName.Length - ext.Length);
								itw.WriteLine ("public enum {0} : uint", enumName);
								itw.WriteLine ("{");
								itw.Indent++;
								foreach (string en in GLEnumGroups[egrp]) {
									glEnumValue eval = null;
									if (edef == null)
										eval = GLEnums.SelectMany (gle => gle.values).
											Where(evals=>evals.name == en).FirstOrDefault();
									else
										eval = edef.values.Where (ev => ev.name == en).FirstOrDefault ();
									if (eval == null) {
										eval = GLEnums.SelectMany (gle => gle.values).
											Where(evals=>evals.name == en).FirstOrDefault();									
										if (eval == null) {
											Console.WriteLine ("enum val for {0} not found in enum values", en);
											continue;
										}
									}
									string enumValueName = ToCamelCase(eval.name);
									int tabs = (int)Math.Max (0, tabsCountForValue - Math.Floor ((double)enumValueName.Length / 4.0));
									itw.WriteLine ("{0}{1}= {2},", enumValueName, new String('\t',tabs) ,eval.value);
								}
								itw.Indent--;
								itw.WriteLine ("}\n");
							}

							if (!string.IsNullOrEmpty (groupedGrp.Key)) {
								itw.Indent--;
								itw.WriteLine ("}\n");
							}
						}

						itw.Indent--;
						itw.WriteLine ("}");
						itw.Indent--;
						itw.WriteLine ("}");
					}
				}
			}
		}
		/*
		static void writeCSEnums () {
			string outputFile = "enums.cs";
			foreach (IGrouping<string,glEnumDef> edg in GLEnums.GroupBy(e=>e.ns)) {
				string ns = edg.Key;
				string outputPath = string.Format (@"generated/{0}", ns);
				Directory.CreateDirectory (outputPath);
				using (TextWriter tw = new StreamWriter(Path.Combine (outputPath,outputFile))) {
					using (IndentedTextWriter itw = new IndentedTextWriter(tw))	{
						itw.WriteLine ("// Autogenerated File");
						itw.WriteLine ("using System;");
						itw.WriteLine ("namespace {0}", rootns);
						itw.WriteLine ("{");
						itw.Indent++;
						itw.WriteLine ("public static partial class {0}", ns);
						itw.WriteLine ("{");
						itw.Indent++;
						foreach (glEnumDef ed in edg) {
							if (ed.bitmask)
								itw.WriteLine ("[Flag]");
							itw.WriteLine ("public enum {0}", ed.group);
							itw.WriteLine ("{");
							itw.Indent++;
							foreach (glEnumValue ev in ed.values) {
								int tabs = (int)Math.Max (0, tabsCountForValue - Math.Floor ((double)ev.name.Length / 4.0));
								itw.WriteLine ("{0}{1}= {2},", ev.name, new String('\t',tabs) ,ev.value);
							}
							itw.Indent--;
							itw.WriteLine ("}\n");
						}
						itw.Indent--;
						itw.WriteLine ("}");
						itw.Indent--;
						itw.WriteLine ("}");
					}
				}
			}			
		}*/
		static void writeCSICalls () {
			string outputFile = "commands.cs";
			foreach (IGrouping<string,glCommand> edg in GLCommands.GroupBy(c=>c.ns)) {
				string ns = edg.Key;
				string outputPath = string.Format (@"generated/{0}", ns);
				Directory.CreateDirectory (outputPath);
				using (TextWriter tw = new StreamWriter(Path.Combine (outputPath,outputFile))) {
					using (IndentedTextWriter itw = new IndentedTextWriter(tw))	{
						itw.WriteLine ("// Autogenerated File");
						itw.WriteLine ("using System;");
						itw.WriteLine ("using System.Runtime.CompilerServices;");
						itw.WriteLine ("namespace {0}", rootns);
						itw.WriteLine ("{");
						itw.Indent++;
						itw.WriteLine ("public static partial class {0}", ns);
						itw.WriteLine ("{");
						itw.Indent++;
						foreach (IGrouping<string,glCommand> groupedCmds in edg.GroupBy (k=>GetExtFromGroupName(k.name))) {
							string ext = groupedCmds.Key;

							if (!string.IsNullOrEmpty (ext)) {
								if (char.IsDigit (ext [0]))
									ext = "_" + ext;
								itw.WriteLine ("public static partial class {0}", ext);
								itw.WriteLine ("{");
								itw.Indent++;
							}
							foreach (glCommand cmd in groupedCmds) {
								string rt = "void";
								string cmdName = cmd.name;
								if (!string.IsNullOrEmpty (ext))
									cmdName = cmdName.Remove (cmdName.Length - ext.Length);
								if (!string.IsNullOrEmpty (cmd.returnType))
									rt = getCSTypeFromGLType(cmd.returnType);
								itw.WriteLine ("[MethodImplAttribute(MethodImplOptions.InternalCall)]");
								itw.Write ("public static extern {0} {1} (", rt, cmdName);
								StringBuilder strparams = new StringBuilder();
								foreach (glParam pm in cmd.paramList) {
									string t = null;
									string n = null;

									if (!string.IsNullOrEmpty (pm.group)) {
										if (GLEnumGroups.ContainsKey (pm.group)) {
											t = pm.group;
											string extt;
											if (TryExtractExt (ref t, out extt))
												t = extt + "." + t;
										}									
									}
									if (t == null) {
										if (!string.IsNullOrEmpty (pm.type)) {										
											t = getCSTypeFromGLType (pm.type);
										} else {
											t = "Array";
											Console.WriteLine ("setting Array for:{0} param in {1}", pm.name, cmd.name);
										}
									}
									if (n == null)
										n = pm.name;
									if (csReservedKeyword.Contains(n))
										n = "_" + n;

									strparams.Append (string.Format ("{0} {1}, ", t, n));
								}
								if (strparams.Length > 0) {								
									strparams.Remove (strparams.Length - 2, 2);
									itw.Write (strparams.ToString ());
								}
								itw.WriteLine (");");
							}
							if (!string.IsNullOrEmpty (groupedCmds.Key)) {
								itw.Indent--;
								itw.WriteLine ("}\n");
							}
						}

						itw.Indent--;
						itw.WriteLine ("}");
						itw.Indent--;
						itw.WriteLine ("}");
					}
				}
			}			
		}
		static void writeCICalls () {
			string outputFile = "glICalls.c";
			foreach (IGrouping<string,glCommand> edg in GLCommands.GroupBy(c=>c.ns)) {
				string ns = edg.Key;
				string outputPath = string.Format (@"generated/{0}", ns);
				Directory.CreateDirectory (outputPath);
				using (TextWriter tw = new StreamWriter(Path.Combine (outputPath,outputFile))) {
					using (IndentedTextWriter itw = new IndentedTextWriter(tw))	{						
						itw.WriteLine ("// Autogenerated File");
						itw.WriteLine ("#include <mono/jit/jit.h>");
						itw.WriteLine ("#include <mono/metadata/environment.h>");
						itw.WriteLine ("#include <GL/gl.h>");
						//itw.WriteLine ("#include <stdlib.h>");
						itw.WriteLine ("void registerGLICalls ()");
						itw.WriteLine ("{");
						itw.Indent++;
						foreach (glCommand cmd in edg) {
							itw.WriteLine ("mono_add_internal_call (\"{0}.{1}::{2}\", {2});",
								rootns, ns, cmd.name);
//							string rt = "void";
//							if (!string.IsNullOrEmpty (cmd.returnType))
//								rt = getCSTypeFromGLType(cmd.returnType);
//							itw.WriteLine ("[MethodImplAttribute(MethodImplOptions.InternalCall)]");
//							itw.Write ("public static extern {0} {1} (", rt, cmd.name);
//							StringBuilder strparams = new StringBuilder();
//							foreach (glParam pm in cmd.paramList) {
//								string t = null;
//								string n = null;
//
//								if (!string.IsNullOrEmpty (pm.group)) {
//									if (GLEnumGroups.ContainsKey (pm.group))
//										t = pm.group;									
//								}
//								if (t == null) {
//									if (!string.IsNullOrEmpty (pm.type)) {										
//										t = getCSTypeFromGLType (pm.type);
//									} else {
//										t = "Array";
//										Console.WriteLine ("setting Array for:{0} param in {1}", pm.name, cmd.name);
//									}
//								}
//								if (n == null)
//									n = pm.name;
//								if (csReservedKeyword.Contains(n))
//									n = "_" + n;
//
//								strparams.Append (string.Format ("{0} {1}, ", t, n));
//							}
//							if (strparams.Length > 0) {								
//								strparams.Remove (strparams.Length - 2, 2);
//								itw.Write (strparams.ToString ());
//							}
//							itw.WriteLine (");");
						}
						itw.Indent--;
						itw.WriteLine ("}");
					}
				}
			}			
		}

		static string ToCamelCase(string str){
			if (string.IsNullOrEmpty (str))
				return null;
			string[] tmps = str.Split (new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

			int ptr = 0;
			if (string.Equals (tmps [0], "gl", StringComparison.OrdinalIgnoreCase))
				ptr = 1;
			string result = "";
			if (char.IsDigit (tmps [ptr] [0]))
				result += "GL";
			while (ptr < tmps.Length) {
				if (tmps [ptr] != "BIT")
					result += char.ToUpper (tmps [ptr] [0]) + tmps [ptr].Substring (1).ToLower();
				ptr++;
			}
			return result;
		}

		public static string getCSName(string str){
			if (string.IsNullOrEmpty (str))
				return null;
			string[] tmps = str.Split (new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

			int ptr = 0;
			if (string.Equals (tmps [0], "gl", StringComparison.OrdinalIgnoreCase))
				ptr = 1;
			string result = "";
			if (char.IsDigit (tmps [ptr] [0]))
				result += "GL";
			while (ptr < tmps.Length) {
				if (tmps [ptr] != "BIT" && tmps [ptr] != "MASK")
					result += char.ToUpper (tmps [ptr] [0]) + tmps [ptr].Substring (1).ToLower();
				ptr++;
			}
			return result;
		}

		public static bool TryExtractExt(ref string name, out string ext) {
			ext = "";
			foreach (string e in MainClass.extensions) {
				if (name.EndsWith (e)) {
					ext = e;
					name = name.Remove (name.Length - ext.Length);
					if (name.EndsWith ("_"))
						name = name.Remove (name.Length - 1);
					return true;
				}					
			}
			return false;
		}
		public static bool TryGetExt(string name, out string ext) {
			ext = "";
			foreach (string e in MainClass.extensions) {
				if (name.EndsWith (e)) {
					ext = e;
					return true;
				}					
			}
			return false;
		}
		public static string GetExtFromGroupName (string name){
			foreach (string e in MainClass.extensions) {
				if (name.EndsWith (e))
					return e;
			}
			return "";
		}

		static void buildGLTypeDic(){
			using (StreamReader sr = new StreamReader ("specs/ctypeTocsharp.txt")) {
				while (!sr.EndOfStream) {
					string[] l = sr.ReadLine ()?.Split (',');
					if (l?.Length != 2)
						throw new Exception ("invalid syntax in 'specs/ctypeTocsharp.txt'");				
					CtoCSTypeDic.Add (l [0].Trim (), l [1].Trim ());
				}
			}
		}

		static string getCSTypeFromGLType (string strglt){
			string strCt = strglt;
			glTypeDef gtd = null;
			while (strCt.StartsWith("GL")){
				gtd = GLTypes.Where (gltt=>gltt.name == strCt).FirstOrDefault();
				if (gtd == null)
					break;
				strCt = gtd.ctype;
			}
			if (gtd == null)
				strCt = strglt;
			
			return CtoCSTypeDic.ContainsKey (strCt) ? CtoCSTypeDic [strCt] : "NOTFOUND_IN_CTOCSDIC_" + strCt;
		}
	}
}
