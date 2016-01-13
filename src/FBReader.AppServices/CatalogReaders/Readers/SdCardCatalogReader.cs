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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FBReader.Common.Exceptions;
using FBReader.DataModel.Model;
using FBReader.PhoneServices;
using FBReader.Render.Parsing;
using FBReader.Tokenizer.Parsers;

namespace FBReader.AppServices.CatalogReaders.Readers
{
    public class SdCardCatalogReader : ISearchableCatalogReader
    {
        private readonly CatalogModel _catalogModel;
        private readonly ISdCardStorage _sdCardStorage;
        private Stack<string> _stack;

        public SdCardCatalogReader(ISdCardStorage sdCardStorage, CatalogModel catalogModel)
        {
            _sdCardStorage = sdCardStorage;
            _catalogModel = catalogModel;
            _stack = new Stack<string>();
        }

        public async Task<IEnumerable<CatalogItemModel>> ReadAsync()
        {
            try
            {
                return await _sdCardStorage.GetFolderAsync(_stack.Any() ? _stack.First() : null).ContinueWith(folderTask =>
                    {
                        var folder = folderTask.Result;
                        var foldersTask = folder.GetFoldersAsync().ContinueWith(task =>
                            task.Result.Select(i => new CatalogItemModel
                            {
                                OpdsUrl = i.Path,
                                Title = i.Name,
                            })
                        );

                        var filesTask = folder.GetFilesAsync().ContinueWith((task) =>
                            {
                                List<Task<CatalogItemModel>> tasks = new List<Task<CatalogItemModel>>();
                                foreach (var file in task.Result)
                                {
                                    var ext = Path.GetExtension(file.Path);
                                    if (ext != null)
                                    {
                                        var type = ext.TrimStart('.').ToLower();
                                        tasks.Add(file.OpenForReadAsync().ContinueWith(fileTask =>
                                            {
                                                var stream = fileTask.Result;
                                                BookSummary preview;
                                                try
                                                {
                                                    var parser = BookFactory.GetPreviewGenerator(type, file.Name, stream);
                                                    preview = parser.GetBookPreview();
                                                }
                                                catch (Exception)
                                                {
                                                    //can't parse book
                                                    Debugger.Break();
                                                    return null;
                                                }
                                                return (CatalogItemModel) new CatalogBookItemModel
                                                {
                                                    Title = preview.Title,
                                                    Description = preview.Description,
                                                    Author = preview.AuthorName,
                                                    Links = new List<BookDownloadLinkModel>
                                                {
                                                    new BookDownloadLinkModel {Type = '.' + type, Url = file.Path}
                                                },
                                                    OpdsUrl = file.Path,
                                                    Id = file.Path
                                                };
                                            }));
                                    }
                                }

                                return Task.WhenAll(tasks.ToArray()).Result.Where(i => i != null);
                            });

                        Task<IEnumerable<CatalogItemModel>>[] allTasks = { foldersTask, filesTask };
                        return Task.WhenAll(allTasks).Result.SelectMany(j => j);
                    });
            }
            catch (SdCardNotSupportedException)
            {
                return Enumerable.Empty<CatalogItemModel>();
            }
        }

        public bool CanReadNextPage
        {
            get
            {
                return false;
            }
        }

        public bool CanGoBack
        {
            get
            {
                return _stack.Any();
            }
        }

        public int CatalogId
        {
            get
            {
                return _catalogModel.Id;
            }
        }

        public Task<IEnumerable<CatalogItemModel>> ReadNextPageAsync()
        {
            throw new NotSupportedException();
        }

        public void GoTo(CatalogItemModel model)
        {
            _stack.Push(model.OpdsUrl);
        }

        public void GoBack()
        {
            if (!_stack.Any())
            {
                throw new ReadCatalogException("Unable go back");
            }
            _stack.Pop();
        }

        public void Refresh()
        {
        }

        public async Task<IEnumerable<CatalogItemModel>> SearchAsync(string query)
        {
            var items = await ReadAsync();
            return items.Where(i => i.Title.ToLower().Contains(query.ToLower()));
        }
    }
}
