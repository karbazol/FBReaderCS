/*
 * Author: CactusSoft (http://cactussoft.biz/), 2013
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
 * 02110-1301, USA.
 */

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FBReader.Common.Exceptions;
using Microsoft.Phone.Storage;

namespace FBReader.PhoneServices
{
    public class SdCardStorage : ISdCardStorage
    {
        private ExternalStorageDevice _sdCardStorage;

        public async Task<bool> GetIsAvailableAsync()
        {
            return null != (await ExternalStorage.GetExternalStorageDevicesAsync()).FirstOrDefault();
        }

        private ExternalStorageDevice GetDevice()
        {
            if (_sdCardStorage == null)
            {
                _sdCardStorage = ExternalStorage.GetExternalStorageDevicesAsync().Result.FirstOrDefault();
            }

            //suppose SD-card is null
            if (_sdCardStorage == null)
            {
                throw new SdCardNotSupportedException();
            }

            return _sdCardStorage;
        }

        public async Task<IEnumerable<ExternalStorageFile>> GetFilesAsync(params string[] extensions)
        {
            //read all files recursively
            return await GetFilesAsync(GetDevice().RootFolder);
        }

        public async Task<IEnumerable<ExternalStorageFile>> GetFilesAsync(ExternalStorageFolder folder)
        {
            return Enumerable.Union(await folder.GetFilesAsync(),
                (await folder.GetFoldersAsync()).SelectMany(i => GetFilesAsync(i).Result));
        }

        public async Task<ExternalStorageFolder> GetFolderAsync(string path)
        {
            if (path == null)
            {
                return GetDevice().RootFolder;
            }
            return await GetDevice().GetFolderAsync(path);
        }
    }
}
