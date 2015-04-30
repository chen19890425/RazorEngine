﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RazorEngine.Templating
{
    /// <summary>
    /// A ResolvePathTemplateManager which watches for changes in the 
    /// filesytem and invalides the corresponding cache entries.
    /// WARNING: 
    /// Use this only on AppDomains you recycle regulary, or to 
    /// improve the debugging experience. 
    /// Never use this in production without any recycle strategy.
    /// </summary>
    public sealed class WatchingResolvePathTemplateManager : ITemplateManager, IDisposable
    {
        private readonly ResolvePathTemplateManager inner;
        private readonly InvalidatingCachingProvider cache;
        private readonly ConcurrentQueue<FileSystemEventArgs> queue = new ConcurrentQueue<FileSystemEventArgs>();
        private readonly List<FileSystemWatcher> watchers;
        private readonly CancellationTokenSource cancelToken = new CancellationTokenSource();
        //private readonly Task queueListenerTask;
        public WatchingResolvePathTemplateManager(IEnumerable<string> layoutRoot, InvalidatingCachingProvider cache)
        {
            this.cache = cache;
            var list = new List<string>(layoutRoot).AsReadOnly();
            inner = new ResolvePathTemplateManager(list);
            watchers = list.Select(path =>
            {
                var watcher = new FileSystemWatcher(Path.GetFullPath(path), "*.*");
                watcher.EnableRaisingEvents = true;
                watcher.IncludeSubdirectories = true;
                watcher.Changed += watcher_Changed;
                watcher.Created += watcher_Changed;
                watcher.Deleted += watcher_Changed;
                watcher.Renamed += watcher_Renamed;
                return watcher;
            }).ToList();
            //queueListenerTask = StartQueue();
        }

        //private async Task StartQueue()
        //{
        //    while (!cancelToken.Token.IsCancellationRequested)
        //    {
        //        if (queue.IsEmpty)
        //        {
        //            await Task.Delay(1000);
        //        }
        //        else
        //        {
        //            FileSystemEventArgs item;
        //            List<FileSystemEventArgs> args = new List<FileSystemEventArgs>();
        //            while (queue.TryDequeue(out item))
        //            {
        //                args.Add(item);
        //            }
        //            var keys =
        //                args.Select(e => );
        //            foreach (var key in keys)
        //            {
        //            }
        //        }
        //    }
        //}

        void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            cache.InvalidateCache(new FullPathTemplateKey(e.Name, e.FullPath, ResolveType.Global, null));
            //queue.Enqueue(e);
        }

        void watcher_Renamed(object sender, RenamedEventArgs e)
        {
            watcher_Changed(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, e.OldFullPath, e.OldName));
            watcher_Changed(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, e.FullPath, e.Name));
        }


        public ITemplateSource Resolve(ITemplateKey key)
        {
            return inner.Resolve(key);
        }

        public ITemplateKey GetKey(string name, ResolveType resolveType, ITemplateKey context)
        {
            return inner.GetKey(name, resolveType, context);
        }

        public void AddDynamic(ITemplateKey key, ITemplateSource source)
        {
            inner.AddDynamic(key, source);
        }

        private bool isDisposed = false;
        public void Dispose()
        {
            if (!isDisposed)
            {
                cancelToken.Cancel();
                foreach (var watcher in watchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                cancelToken.Dispose();
                //queueListenerTask.Wait(5000);
                isDisposed = true;
            }
        }
    }
}