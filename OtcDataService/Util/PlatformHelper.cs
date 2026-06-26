#region <<Notes>>
/*----------------------------------------------------------------
 * Copy right (c) 2026  All rights reserved。
 * CLR Ver: 4.0.30319.42000
 * Computer: MOLEQ-MING
 * Company: 
 * namespace: OtcDataService.Util
 * Unique: 041c09aa-7766-467d-88e9-dc89c50711ec
 * File name: PlatformHelper
 * Domain: MOLEQ-MING
 * 
 * @author: t8min
 * @email: t8ming@live.com
 * @date: 6/26/2026 11:44:37
 *----------------------------------------------------------------*/
#endregion <<Notes>>
using OtcDataService.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OtcDataService.Util
{
    public static class PlatformHelper
    {
        public static int GetDoubleClickTime()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsDoubleClickTime();
            }

            return 500;
        }

        private static int GetWindowsDoubleClickTime()
        {
            return (int)Win32User32.GetDoubleClickTime();
        }
    }
}
