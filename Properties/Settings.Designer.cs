﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ClipboardAccelerator.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "14.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("110")]
        public uint ClipHistory {
            get {
                return ((uint)(this["ClipHistory"]));
            }
            set {
                this["ClipHistory"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool bEnableFirstLineOnly {
            get {
                return ((bool)(this["bEnableFirstLineOnly"]));
            }
            set {
                this["bEnableFirstLineOnly"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public uint uiExecutionWarningCount {
            get {
                return ((uint)(this["uiExecutionWarningCount"]));
            }
            set {
                this["uiExecutionWarningCount"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("500000")]
        public uint uiClipDisplaySize {
            get {
                return ((uint)(this["uiClipDisplaySize"]));
            }
            set {
                this["uiClipDisplaySize"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("500")]
        public double dClipboardDelay {
            get {
                return ((double)(this["dClipboardDelay"]));
            }
            set {
                this["dClipboardDelay"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("3000")]
        public uint uiNotificationWNDDelay {
            get {
                return ((uint)(this["uiNotificationWNDDelay"]));
            }
            set {
                this["uiNotificationWNDDelay"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("22")]
        public uint uiCommandsFontSize {
            get {
                return ((uint)(this["uiCommandsFontSize"]));
            }
            set {
                this["uiCommandsFontSize"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool bHideFromTaskbarOnMinimize {
            get {
                return ((bool)(this["bHideFromTaskbarOnMinimize"]));
            }
            set {
                this["bHideFromTaskbarOnMinimize"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public double dXNotifyWindow {
            get {
                return ((double)(this["dXNotifyWindow"]));
            }
            set {
                this["dXNotifyWindow"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public double dYNotifyWindow {
            get {
                return ((double)(this["dYNotifyWindow"]));
            }
            set {
                this["dYNotifyWindow"] = value;
            }
        }
    }
}