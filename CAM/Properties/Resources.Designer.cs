﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.269
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace CAM.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("CAM.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Pause.
        /// </summary>
        internal static string AnimationPause {
            get {
                return ResourceManager.GetString("AnimationPause", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Play.
        /// </summary>
        internal static string AnimationPlay {
            get {
                return ResourceManager.GetString("AnimationPlay", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Step.
        /// </summary>
        internal static string AnimationStep {
            get {
                return ResourceManager.GetString("AnimationStep", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Create and edit UV tool paths..
        /// </summary>
        internal static string FaceToolPathToolButtonHint {
            get {
                return ResourceManager.GetString("FaceToolPathToolButtonHint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UV.
        /// </summary>
        internal static string FaceToolPathToolButtonText {
            get {
                return ResourceManager.GetString("FaceToolPathToolButtonText", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot; ?&gt;
        ///&lt;customUI xmlns=&quot;http://schemas.spaceclaim.com/customui&quot;&gt;
        ///  &lt;panel&gt;
        ///    &lt;group id=&quot;FaceToolPathToolGroup&quot; label=&quot;Profile Options&quot;&gt;
        ///      &lt;container id=&quot;FaceToolPathToolContainer&quot; layoutOrientation=&quot;vertical&quot;&gt;
        ///        &lt;container id=&quot;FaceToolPathToolStrategyContainer&quot; layoutOrientation=&quot;horizontal&quot;&gt;
        ///          &lt;label id=&quot;FaceToolPathToolStrategyLabel&quot; text=&quot;Strategy:&quot; width=&quot;40&quot;/&gt;
        ///          &lt;comboBox id=&quot;FaceToolPathToolStrategyList&quot; command=&quot;FaceToolPathToolStrat [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string FaceToolPathToolOptions {
            get {
                return ResourceManager.GetString("FaceToolPathToolOptions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Select a face to create a tool path, or select a tool path to edit it..
        /// </summary>
        internal static string FaceToolPathToolStatusText {
            get {
                return ResourceManager.GetString("FaceToolPathToolStatusText", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Step Over.
        /// </summary>
        internal static string StepOver {
            get {
                return ResourceManager.GetString("StepOver", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CAM.
        /// </summary>
        internal static string TabText {
            get {
                return ResourceManager.GetString("TabText", resourceCulture);
            }
        }
        
        internal static System.Drawing.Bitmap ToolPath32 {
            get {
                object obj = ResourceManager.GetObject("ToolPath32", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tool Path.
        /// </summary>
        internal static string ToolPathGroupText {
            get {
                return ResourceManager.GetString("ToolPathGroupText", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to .
        /// </summary>
        internal static string UVPathName {
            get {
                return ResourceManager.GetString("UVPathName", resourceCulture);
            }
        }
        
        internal static System.Drawing.Bitmap UVToolPath {
            get {
                object obj = ResourceManager.GetObject("UVToolPath", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        internal static System.Drawing.Bitmap UVToolPathDisabled {
            get {
                object obj = ResourceManager.GetObject("UVToolPathDisabled", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
    }
}
