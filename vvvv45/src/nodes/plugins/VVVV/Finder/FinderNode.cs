#region usings
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Practices.Unity;
using VVVV.Core;
using VVVV.Core.Menu;
using VVVV.Core.View;
using VVVV.HDE.Viewer;
using VVVV.HDE.Viewer.WinFormsViewer;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

#endregion usings

namespace VVVV.Nodes.Finder
{
	public enum SearchScope {Global, Local, Downstream};
	
	[PluginInfo(Name = "Finder",
	            Category = "HDE",
	            Shortcut = "Ctrl+F",
	            Author = "vvvv group",
	            Help = "Finds Nodes, Comments and Send/Receive channels and more.",
	            InitialBoxWidth = 200,
	            InitialBoxHeight = 250,
	            InitialWindowWidth = 320,
	            InitialWindowHeight = 500,
	            InitialComponentMode = TComponentMode.InAWindow)]
	public class FinderPluginNode: UserControl, IPluginHDE, INodeListener
	{
		#region field declaration
		private IDiffSpread<string> FTagsPin;
		
		private IPluginHost2 FPluginHost;
		private IHDEHost FHDEHost;
		private MappingRegistry FMappingRegistry;
		private List<PatchNode> FPlainResultList = new List<PatchNode>();
		private SearchScope FSearchScope = SearchScope.Local;
		
		private IWindow FActivePatchWindow;
		private IWindow FActiveWindow;
		private PatchNode FSearchResult;
		private PatchNode FActivePatchNode;
		private PatchNode FRoot;
		
		private bool FSendReceive;
		private bool FComments;
		private bool FLabels;
		private bool FEffects;
		private bool FFreeframes;
		private bool FModules;
		private bool FPlugins;
		private bool FIONodes;
		private bool FNatives;
		private bool FVSTs;
		private bool FPatches;
		private bool FUnknowns;
		private bool FBoygrouped;
		private bool FAddons;
		private bool FWindows;
		private bool FIDs;
		
		private INode FActivePatchParent;
		private List<string> FTags;
		private int FSearchIndex;
		private List<INode> FNodes = new List<INode>();
		
		// Track whether Dispose has been called.
		private bool FDisposed = false;
		#endregion field declaration
		
		#region constructor/destructor
		[ImportingConstructor]
		public FinderPluginNode(IHDEHost host, IPluginHost2 pluginHost, [Config("Tags")] IDiffSpread<string> tagsPin)
		{
			// The InitializeComponent() call is required for Windows Forms designer support.
			InitializeComponent();
			
			FHDEHost = host;
			FPluginHost = pluginHost;
			
			FSearchTextBox.ContextMenu = new ContextMenu();
			FSearchTextBox.ContextMenu.Popup += FSearchTextBox_ContextMenu_Popup;
			FSearchTextBox.MouseWheel += FSearchTextBox_MouseWheel;
			
			FMappingRegistry = new MappingRegistry();
			FMappingRegistry.RegisterDefaultMapping<INamed, DefaultNameProvider>();
			FHierarchyViewer.Registry = FMappingRegistry;
			
			FHDEHost.WindowSelectionChanged += FHDEHost_WindowSelectionChanged;
			//defer setting the active patch window as
			//this will trigger the initial WindowSelectionChangeCB
			//which will want to access this windows caption which is not yet available
			SynchronizationContext.Current.Post((object state) => FHDEHost_WindowSelectionChanged(FHDEHost, new WindowEventArgs(FHDEHost.ActivePatchWindow)), null);
			
			FTagsPin = tagsPin;
			FTagsPin.Changed += FTagsPin_Changed;
		}

		private void InitializeComponent()
		{
			this.panel1 = new System.Windows.Forms.Panel();
			this.panel3 = new System.Windows.Forms.Panel();
			this.FSearchTextBox = new System.Windows.Forms.TextBox();
			this.panel2 = new System.Windows.Forms.Panel();
			this.FNodeCountLabel = new System.Windows.Forms.Label();
			this.FHierarchyViewer = new VVVV.HDE.Viewer.WinFormsViewer.HierarchyViewer();
			this.panel1.SuspendLayout();
			this.panel3.SuspendLayout();
			this.SuspendLayout();
			// 
			// panel1
			// 
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.panel3);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
			this.panel1.Location = new System.Drawing.Point(0, 0);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(252, 17);
			this.panel1.TabIndex = 7;
			// 
			// panel3
			// 
			this.panel3.Controls.Add(this.FSearchTextBox);
			this.panel3.Controls.Add(this.panel2);
			this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel3.Location = new System.Drawing.Point(0, 0);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(250, 15);
			this.panel3.TabIndex = 11;
			// 
			// FSearchTextBox
			// 
			this.FSearchTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
			this.FSearchTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.FSearchTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.FSearchTextBox.Location = new System.Drawing.Point(2, 0);
			this.FSearchTextBox.MinimumSize = new System.Drawing.Size(0, 17);
			this.FSearchTextBox.Name = "FSearchTextBox";
			this.FSearchTextBox.Size = new System.Drawing.Size(248, 17);
			this.FSearchTextBox.TabIndex = 13;
			this.FSearchTextBox.TextChanged += new System.EventHandler(this.FSearchTextBoxTextChanged);
			this.FSearchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FSearchTextBoxKeyDown);
			// 
			// panel2
			// 
			this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
			this.panel2.Dock = System.Windows.Forms.DockStyle.Left;
			this.panel2.Location = new System.Drawing.Point(0, 0);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(2, 15);
			this.panel2.TabIndex = 11;
			// 
			// FNodeCountLabel
			// 
			this.FNodeCountLabel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
			this.FNodeCountLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.FNodeCountLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.FNodeCountLabel.Location = new System.Drawing.Point(0, 256);
			this.FNodeCountLabel.Name = "FNodeCountLabel";
			this.FNodeCountLabel.Size = new System.Drawing.Size(252, 17);
			this.FNodeCountLabel.TabIndex = 8;
			this.FNodeCountLabel.Text = "Matching Nodes: ";
			// 
			// FHierarchyViewer
			// 
			this.FHierarchyViewer.Dock = System.Windows.Forms.DockStyle.Fill;
			this.FHierarchyViewer.Location = new System.Drawing.Point(0, 17);
			this.FHierarchyViewer.Name = "FHierarchyViewer";
			this.FHierarchyViewer.ShowLinks = false;
			this.FHierarchyViewer.Size = new System.Drawing.Size(252, 239);
			this.FHierarchyViewer.TabIndex = 9;
			this.FHierarchyViewer.DoubleClick += new VVVV.HDE.Viewer.WinFormsViewer.ClickHandler(this.FHierarchyViewerDoubleClick);
			this.FHierarchyViewer.Click += new VVVV.HDE.Viewer.WinFormsViewer.ClickHandler(this.FHierarchyViewerClick);
			this.FHierarchyViewer.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.FHierarchyViewerKeyPress);
			// 
			// FinderPluginNode
			// 
			this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
			this.Controls.Add(this.FHierarchyViewer);
			this.Controls.Add(this.FNodeCountLabel);
			this.Controls.Add(this.panel1);
			this.DoubleBuffered = true;
			this.Name = "FinderPluginNode";
			this.Size = new System.Drawing.Size(252, 273);
			this.panel1.ResumeLayout(false);
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			this.ResumeLayout(false);
		}
		private System.Windows.Forms.Panel panel2;
		private System.Windows.Forms.Panel panel3;
		private VVVV.HDE.Viewer.WinFormsViewer.HierarchyViewer FHierarchyViewer;
		private System.Windows.Forms.Label FNodeCountLabel;
		private System.Windows.Forms.TextBox FSearchTextBox;
		private System.Windows.Forms.Panel panel1;
		
		// Dispose(bool disposing) executes in two distinct scenarios.
		// If disposing equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be disposed.
		// If disposing equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be disposed.
		protected override void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.
			if(!FDisposed)
			{
				if(disposing)
				{
					// Dispose managed resources.
					FSearchTextBox.ContextMenu.Popup -= FSearchTextBox_ContextMenu_Popup;
					FSearchTextBox.MouseWheel -= FSearchTextBox_MouseWheel;
					FHDEHost.WindowSelectionChanged -= FHDEHost_WindowSelectionChanged;
					FTagsPin.Changed -= FTagsPin_Changed;
					
					if (FRoot != FActivePatchNode)
						FRoot.Dispose();
					
					if (FActivePatchNode != null)
						FActivePatchNode.Dispose();
					
					if (FSearchResult != null)
						FSearchResult.Dispose();
					
					this.FSearchTextBox.TextChanged -= this.FSearchTextBoxTextChanged;
					this.FSearchTextBox.KeyDown -= this.FSearchTextBoxKeyDown;
					
					this.FHierarchyViewer.DoubleClick -= this.FHierarchyViewerDoubleClick;
					this.FHierarchyViewer.Click -= this.FHierarchyViewerClick;
					this.FHierarchyViewer.KeyPress -= this.FHierarchyViewerKeyPress;
					this.FHierarchyViewer.Dispose();
					this.FHierarchyViewer = null;
					
					FMappingRegistry.Container.Dispose();
				}
				// Release unmanaged resources. If disposing is false,
				// only the following code is executed.
				
				// Note that this is not thread safe.
				// Another thread could start disposing the object
				// after the managed resources are disposed,
				// but before the disposed flag is set to true.
				// If thread safety is necessary, it must be
				// implemented by the client.
			}
			FDisposed = true;
		}
		
		#endregion constructor/destructor
		
		void FTagsPin_Changed(IDiffSpread<string> spread)
		{
			FSearchTextBox.Text = spread[0];
		}
		
		#region IWindowSelectionListener
		void FHDEHost_WindowSelectionChanged(object sender, WindowEventArgs args)
		{
			var window = args.Window;
			
			if (window == FActiveWindow)
				return;
			
			FActiveWindow = window;
			var windowType = window.GetWindowType();
			var updateActiveWindow = false;
			
			if (windowType == WindowType.Module || windowType == WindowType.Patch)
			{
				if (window != FActivePatchWindow)
				{
					if (FActivePatchNode != null)
						FActivePatchNode.Dispose();
					if (FActivePatchParent != null)
						FActivePatchParent.RemoveListener(this);
					
					FActivePatchNode = new PatchNode(window.GetNode());
					
					//the hosts window may be null if the plugin is created hidden on startup
					if (FPluginHost.Window != null)
						FPluginHost.Window.Caption = FActivePatchNode.Name;
					
					FActivePatchParent = FindParent(FHDEHost.Root, window.GetNode());
					//if this is not the root
					if (FActivePatchParent != null)
						FActivePatchParent.AddListener(this);
					
					UpdateSearch();
					
					FActivePatchWindow = window;
				}
				else
					updateActiveWindow = true;
			}
			else
			{
				//activepatchwindow may no longer exist when a window other than a patch is activated
				//this may be due to all patches have been deleted
				if (FActivePatchWindow != FHDEHost.ActivePatchWindow)
				{
					ClearSearch();
					if (FActivePatchNode != null)
						FActivePatchNode.Dispose();
					if (FActivePatchParent != null)
						FActivePatchParent.RemoveListener(this);
				}
				updateActiveWindow = true;
			}
			
			if (updateActiveWindow && FSearchResult != null)
			{
				FSearchResult.SetActiveWindow(FActiveWindow);
				FHierarchyViewer.Redraw();
			}
		}
		#endregion IWindowSelectionListener
		
		#region INodeListener
		public void AddedCB(INode childNode)
		{
			//do nothing;
		}
		
		public void RemovedCB(INode childNode)
		{
			//if active patch is being deleted
			//detach view
			if (childNode == FActivePatchNode.Node)
			{
				FActivePatchNode.Dispose();
				FActivePatchNode = null;
				FActivePatchWindow = null;
				FActiveWindow = null;
				
				if (FActivePatchParent != null)
				{
					FActivePatchParent.RemoveListener(this);
					FActivePatchParent = null;
				}
				
				if (FSearchScope != SearchScope.Global)
					ClearSearch();
				//FHierarchyViewer.UpdateView();
			}
		}
		
		public void LabelChangedCB()
		{
			//do nothing
		}
		#endregion INodeListener
		
		#region Search
		void FSearchTextBoxTextChanged(object sender, EventArgs e)
		{
			UpdateSearch();
			
			//save tags in config pin
			FTagsPin[0] = FSearchTextBox.Text;
		}
		
		void FSearchTextBoxKeyDown(object sender, KeyEventArgs e)
		{
			if (FPlainResultList.Count == 0)
				return;
			
			if (e.KeyCode == Keys.F3 || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
			{
				FPlainResultList[FSearchIndex].Selected = false;
				if (e.Shift || e.KeyCode == Keys.Up)
				{
					FSearchIndex -= 1;
					if (FSearchIndex < 0)
						FSearchIndex = FPlainResultList.Count - 1;
				}
				else
					FSearchIndex = (FSearchIndex + 1) % FPlainResultList.Count;
				
				FPlainResultList[FSearchIndex].Selected = true;
				
				//select the node
				FHDEHost.SelectNodes(new INode[1]{FPlainResultList[FSearchIndex].Node});
				
				FHierarchyViewer.Redraw();
			}
			else if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Enter)
			{
				OpenPatch(FPlainResultList[FSearchIndex].Node);
			}
		}
		
		private void AddNodesByTag(PatchNode searchResult, PatchNode sourceTree)
		{
			//go through child nodes of sourceTree recursively and see if any contains the tag
			foreach (PatchNode node in sourceTree.ChildNodes)
			{
				//now first go downstream recursively
				//to see if this pn is needed in the hierarchy to hold any matching downstream nodes
				//create a dummy to attach possible matching downstream nodes
				var parent = new PatchNode();
				parent.Node = node.Node;
				AddNodesByTag(parent, node);
				
				var include = CheckForInclusion(parent);
				if (parent.ChildNodes.Count > 0 || include)
				{
					searchResult.ChildNodes.Add(parent);
					if (include)
						FPlainResultList.Add(parent);
				}
				else
					parent.Dispose();
			}
		}
		
		private bool CheckForInclusion(PatchNode node)
		{
			bool include = false;
			var quickTagsUsed = FSendReceive || FComments || FLabels || FIONodes || FNatives || FModules || FEffects || FFreeframes || FVSTs || FPlugins || FPatches || FUnknowns || FAddons || FBoygrouped || FWindows || FIDs;
			
			if (FTags.Count == 0)
			{
				if (quickTagsUsed)
				{
					include = FSendReceive && !string.IsNullOrEmpty(node.SRChannel);
					include |= FComments && !string.IsNullOrEmpty(node.Comment);
					include |= FLabels && !string.IsNullOrEmpty(node.DescriptiveName);
					include |= FIONodes && node.IsIONode;
					include |= FNatives && node.NodeType == NodeType.Native;
					include |= FModules && node.NodeType == NodeType.Module;
					include |= FEffects && node.NodeType == NodeType.Effect;
					include |= FFreeframes && node.NodeType == NodeType.Freeframe;
					include |= FVSTs && node.NodeType == NodeType.VST;
					include |= FPlugins && (node.NodeType == NodeType.Plugin || node.NodeType == NodeType.Dynamic);
					include |= FPatches && node.NodeType == NodeType.Patch;
					include |= FUnknowns && node.IsMissing;
					include |= FBoygrouped && node.IsBoygrouped;
					include |= FAddons && (node.NodeType != NodeType.Native && node.NodeType != NodeType.Text && node.NodeType != NodeType.Patch);
					include |= FWindows && (node.Node.HasGUI() || (node.Node.HasPatch() && node.NodeType != NodeType.Module));
				}
				else
					include = true;
			}
			else
			{
				if (FSendReceive && !string.IsNullOrEmpty(node.SRChannel))
				{
					var inc = true;
					var channel = node.SRChannel.ToLower();
					
					foreach (string tag in FTags)
						inc = inc && channel.Contains(tag);
					include |= inc;
				}
				if (FIDs)
				{
					var inc = true;
					var id = node.ID;
					
					foreach (string tag in FTags)
						inc = inc && int.Parse(tag) == id;
					include |= inc;
				}
				if (FComments && !string.IsNullOrEmpty(node.Comment))
				{
					var inc = true;
					var comment = node.Comment.ToLower();
					
					foreach (string tag in FTags)
						inc = inc && comment.Contains(tag);
					include |= inc;
				}
				if (FIONodes && node.IsIONode)
				{
					var inc = true;
					var dname = node.DescriptiveName.ToLower();
					
					foreach (string tag in FTags)
						inc = inc && dname.Contains(tag);
					include |= inc;
				}
				if (FLabels && !string.IsNullOrEmpty(node.DescriptiveName))
				{
					var inc = true;
					var dname = node.DescriptiveName.ToLower();
					
					foreach (string tag in FTags)
						inc = inc && dname.Contains(tag);
					include |= inc;
				}
				if ((FEffects && node.NodeType == NodeType.Effect)
				    || (FModules && node.NodeType == NodeType.Module)
				    || (FPlugins && (node.NodeType == NodeType.Plugin || node.NodeType == NodeType.Dynamic))
				    || (FFreeframes && node.NodeType == NodeType.Freeframe)
				    || (FNatives && node.NodeType == NodeType.Native)
				    || (FVSTs && node.NodeType == NodeType.VST)
				    || (FPatches && node.NodeType == NodeType.Patch)
				    || (FUnknowns && node.IsMissing)
				    || (FBoygrouped && node.IsBoygrouped)
				    || (FAddons && (node.NodeType != NodeType.Native && node.NodeType != NodeType.Text && node.NodeType != NodeType.Patch))
				    || (FWindows && (node.Node.HasGUI() || (node.Node.HasPatch() && node.NodeType != NodeType.Module))))
				{
					var inc = true;
					var name = node.Name.ToLower();
					
					foreach (string tag in FTags)
						inc = inc && name.Contains(tag);
					include |= inc;
				}
				
				//if non of the one-character tags is chosen
				if (!quickTagsUsed)
				{
					var inc = true;
					var name = node.Name.ToLower();
					
					foreach (string tag in FTags)
						inc = inc && name.Contains(tag);
					include |= inc;
				}
			}
			
			if (include)
				node.SetTags(FTags);
			
			return include;
		}
		
		private void ClearSearch()
		{
			if (FSearchResult != null)
				FSearchResult.Dispose();
			FPlainResultList.Clear();
			FSearchIndex = 0;
			
			FSearchResult = new PatchNode();
		}
		
		private void UpdateSearch()
		{
			//we only need to get the root once
			//in the constructor it is too early since finder might be placed in root
			//and constructor of finder would be called before root was available
			if (FRoot == null && FHDEHost.Root != null)
				FRoot = new PatchNode(FHDEHost.Root);
			
			if (FRoot == null)
				return;
			
			string query = FSearchTextBox.Text.ToLower();
			query += (char) 160;
			
			FHierarchyViewer.ShowLinks = false;
			
			//check for tags in query:
			//g: global
			//d: downstream
			//default scope: local
			FSearchScope = SearchScope.Local;
			FTags = query.Split(new char[1]{' '}).ToList();
			
			if (FTags.Contains("g"))
			{
				FSearchScope = SearchScope.Global;
				FTags.Remove("g");
			}
			else if (FTags.Contains("d"))
			{
				FSearchScope = SearchScope.Downstream;
				FTags.Remove("d");
			}
			
			FSendReceive = FComments = FLabels = FEffects = FFreeframes = FModules = FPlugins = FIONodes = FNatives = FVSTs = FAddons = FUnknowns = FPatches = FBoygrouped = FWindows = FIDs = false;
			if (FTags.Contains("s"))
			{
				FSendReceive = true;
				FTags.Remove("s");
				FHierarchyViewer.ShowLinks = true;
			}
			if (FTags.Contains("/"))
			{
				FComments = true;
				FTags.Remove("/");
			}
			if (FTags.Contains("l"))
			{
				FLabels = true;
				FTags.Remove("l");
			}
			if (FTags.Contains("x"))
			{
				FEffects = true;
				FTags.Remove("x");
			}
			if (FTags.Contains("f"))
			{
				FFreeframes = true;
				FTags.Remove("f");
			}
			if (FTags.Contains("m"))
			{
				FModules = true;
				FTags.Remove("m");
			}
			if (FTags.Contains("p"))
			{
				FPlugins = true;
				FTags.Remove("p");
			}
			if (FTags.Contains("i"))
			{
				FIONodes = true;
				FTags.Remove("i");
			}
			if (FTags.Contains("n"))
			{
				FNatives = true;
				FTags.Remove("n");
			}
			if (FTags.Contains("v"))
			{
				FVSTs = true;
				FTags.Remove("v");
			}
			if (FTags.Contains("t"))
			{
				FPatches = true;
				FTags.Remove("t");
			}
			if (FTags.Contains("r"))
			{
				FUnknowns = true;
				FTags.Remove("r");
			}
			if (FTags.Contains("a"))
			{
				FAddons = true;
				FTags.Remove("a");
			}
			if (FTags.Contains("b"))
			{
				FBoygrouped = true;
				FTags.Remove("b");
			}
			if (FTags.Contains("w"))
			{
				FWindows = true;
				FTags.Remove("w");
			}
			if (FTags.Contains("#"))
			{
				FIDs = true;
				FTags.Remove("#");
			}
			
			//clean up the list
			FTags[FTags.Count-1] = FTags[FTags.Count-1].Trim((char) 160);
			while (FTags.Contains(" "))
				FTags.Remove(" ");
			if (FTags.Contains(""))
				FTags.Remove("");
			
			FHierarchyViewer.BeginUpdate();
			try
			{
				ClearSearch();
				
				switch (FSearchScope)
				{
					case SearchScope.Global:
						{
							//go through child nodes of FRoot recursively and see if any contains the tag
							FSearchResult.Node = FRoot.Node;
							AddNodesByTag(FSearchResult, FRoot);
							break;
						}
					case SearchScope.Local:
						{
							//if there is no active patch, break out
							if (FActivePatchNode == null)
								break;
							
							FSearchResult.Node = FActivePatchNode.Node;
							
							//go through child nodes of FActivePatch and see if any contains the tag
							foreach (PatchNode pn in FActivePatchNode.ChildNodes)
								if (CheckForInclusion(pn))
							{
								var node = new PatchNode();
								node.Node = pn.Node;
								FSearchResult.ChildNodes.Add(node);
								
								FPlainResultList.Add(node);
							}
							break;
						}
					case SearchScope.Downstream:
						{
							//go through child nodes of FActivePatch recursively and see if any contains the tag
							FSearchResult.Node = FActivePatchNode.Node;
							AddNodesByTag(FSearchResult, FActivePatchNode);
							break;
						}
				}
				
				FSearchResult.SetActiveWindow(FActiveWindow);

				FHierarchyViewer.Input = FSearchResult;

				FNodeCountLabel.Text = "Matching Nodes: " + FPlainResultList.Count.ToString();
			}
			finally
			{
				FHierarchyViewer.EndUpdate();
			}
		}
		#endregion Search
		
		#region GUI events
		void FSearchTextBox_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			FHierarchyViewer.Focus();
		}

		void FSearchTextBox_ContextMenu_Popup(object sender, EventArgs e)
		{
			FSearchTextBox.Text = "";
		}
		
		void FHierarchyViewerClick(IModelMapper sender, System.Windows.Forms.MouseEventArgs e)
		{
			if (e.Button == 0)
			{
				(sender.Model as PatchNode).Selected = true;
				FHDEHost.SelectNodes(new INode[1]{(sender.Model as PatchNode).Node});
				
				//only fit view to selected node if not in local scope
				if (FSearchScope != SearchScope.Local)
					if (sender.CanMap<ICamera>())
						sender.Map<ICamera>().View(sender.Model);
			}
			else if ((int)e.Button == 1)
			{
				if (sender.CanMap<ICamera>())
					sender.Map<ICamera>().ViewAll();
			}
			else if ((int)e.Button == 2)
			{
				//only fit view to selected nodes parent if not in local scope
				if (FSearchScope != SearchScope.Local)
					if (sender.CanMap<ICamera>())
						sender.Map<ICamera>().ViewParent(sender.Model);
			}
		}
		
		void FHierarchyViewerDoubleClick(IModelMapper sender, System.Windows.Forms.MouseEventArgs e)
		{
			OpenPatch((sender.Model as PatchNode).Node);
		}
		
		void FHierarchyViewerKeyPress(object sender, KeyPressEventArgs e)
		{
			FSearchTextBox.Focus();
			FSearchTextBox.Text += (e.KeyChar).ToString();
			FSearchTextBox.Select(FSearchTextBox.Text.Length, 1);
		}
		#endregion GUI events
		
		private void OpenPatch(INode node)
		{
			if (node == null)
				FHDEHost.ShowEditor(FindParent(FRoot.Node, FActivePatchNode.Node));
			else if (node.HasPatch())
				FHDEHost.ShowEditor(node);
			else
			{
				FHDEHost.ShowEditor(FindParent(FRoot.Node, node));
				FHDEHost.SelectNodes(new INode[1]{node});
			}
		}
		
		private INode FindParent(INode sourceTree, INode node)
		{
			INode[] children = sourceTree.GetChildren();
			
			if (children != null)
			{
				foreach(INode child in children)
				{
					if (child == node)
						return sourceTree;
					else
					{
						INode p = FindParent(child, node);
						if (p != null)
							return p;
					}
				}
				return null;
			}
			else
				return null;
		}
	}
}
