using System;
using System.Runtime.InteropServices;

namespace corenes
{
    internal static class MacOSMenu
    {
        // Objective-C runtime functions
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
        private static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
        private static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_void(IntPtr receiver, IntPtr selector, IntPtr arg1);

        public static bool IsAvailable()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }

        public static void CreateApplicationMenu()
        {
            if (!IsAvailable())
                return;

            try
            {
                // Get NSApplication
                IntPtr nsApp = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));

                // Create main menu bar
                IntPtr menuBar = objc_msgSend(objc_getClass("NSMenu"), sel_registerName("new"));
                objc_msgSend_void(nsApp, sel_registerName("setMainMenu:"), menuBar);

                // Create "CoreNES" menu (app menu)
                CreateAppMenu(menuBar, nsApp);

                // Create "File" menu
                CreateFileMenu(menuBar);

                Console.WriteLine("macOS menu bar created");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create macOS menu: {ex.Message}");
            }
        }

        private static void CreateAppMenu(IntPtr menuBar, IntPtr nsApp)
        {
            // Create app menu item
            IntPtr appMenuItem = objc_msgSend(objc_getClass("NSMenuItem"), sel_registerName("new"));
            objc_msgSend(menuBar, sel_registerName("addItem:"), appMenuItem);

            // Create app submenu
            IntPtr appMenu = objc_msgSend(objc_getClass("NSMenu"), sel_registerName("new"));
            objc_msgSend(appMenuItem, sel_registerName("setSubmenu:"), appMenu);

            // Add "Quit CoreNES" menu item
            IntPtr quitTitle = CreateNSString("Quit CoreNES");
            IntPtr quitKeyEquiv = CreateNSString("q");
            IntPtr quitItem = objc_msgSend(
                objc_getClass("NSMenuItem"),
                sel_registerName("alloc")
            );
            quitItem = objc_msgSend(
                objc_msgSend(quitItem, sel_registerName("initWithTitle:action:keyEquivalent:"),
                    quitTitle,
                    sel_registerName("terminate:"),
                    quitKeyEquiv),
                sel_registerName("autorelease")
            );
            objc_msgSend(quitItem, sel_registerName("setTarget:"), nsApp);
            objc_msgSend(appMenu, sel_registerName("addItem:"), quitItem);
        }

        private static void CreateFileMenu(IntPtr menuBar)
        {
            // Create "File" menu
            IntPtr fileMenuItem = objc_msgSend(objc_getClass("NSMenuItem"), sel_registerName("new"));
            objc_msgSend(menuBar, sel_registerName("addItem:"), fileMenuItem);

            IntPtr fileMenuTitle = CreateNSString("File");
            IntPtr fileMenu = objc_msgSend(
                objc_msgSend(objc_getClass("NSMenu"), sel_registerName("alloc")),
                sel_registerName("initWithTitle:"),
                fileMenuTitle
            );
            objc_msgSend(fileMenuItem, sel_registerName("setSubmenu:"), fileMenu);

            // Add "Open ROM..." menu item (Cmd+O)
            // Note: The actual action is handled by SDL keyboard events
            IntPtr openTitle = CreateNSString("Open ROM...");
            IntPtr openKeyEquiv = CreateNSString("o");
            IntPtr openItem = CreateMenuItem(openTitle, IntPtr.Zero, openKeyEquiv);
            objc_msgSend(fileMenu, sel_registerName("addItem:"), openItem);
        }

        private static IntPtr CreateMenuItem(IntPtr title, IntPtr action, IntPtr keyEquiv)
        {
            IntPtr item = objc_msgSend(objc_getClass("NSMenuItem"), sel_registerName("alloc"));
            item = objc_msgSend(item, sel_registerName("initWithTitle:action:keyEquivalent:"), title, action, keyEquiv);
            return objc_msgSend(item, sel_registerName("autorelease"));
        }

        private static IntPtr CreateNSString(string str)
        {
            IntPtr nsString = objc_msgSend(objc_getClass("NSString"), sel_registerName("alloc"));
            IntPtr utf8 = Marshal.StringToHGlobalAuto(str);
            nsString = objc_msgSend(nsString, sel_registerName("initWithUTF8String:"), utf8);
            Marshal.FreeHGlobal(utf8);
            return objc_msgSend(nsString, sel_registerName("autorelease"));
        }
    }
}
