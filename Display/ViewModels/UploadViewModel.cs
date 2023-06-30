﻿using CommunityToolkit.Mvvm.ComponentModel;
using Display.Models;
using Display.Services.Upload;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Display.ViewModels
{
    internal class UploadViewModel : ObservableObject
    {
        private const int MaxUploadCount = 3;

        private static UploadViewModel _uploadVm;
        public static UploadViewModel Instance => _uploadVm ??= new UploadViewModel();

        public ObservableCollection<UploadSubItem> UploadCollection = new();

        public void AddUploadTask(string filePath, long cid, Action<FileUploadResult> finishAction = null)
        {
            if (UploadCollection.Any(x => x.UploadFilePath == filePath)) return;

            var uploadUi = new UploadSubItem(filePath, cid);
            UploadCollection.Add(uploadUi);

            uploadUi.UploadFinish += result =>
            {
                finishAction?.Invoke(result);
            };

            uploadUi.RunChanged += running =>
            {
                // 一旦不再running，马上分配任务
                if (!running)
                {
                    SequentialStart();
                }
            };

            SequentialStart();
        }

        /// <summary>
        /// 分配上传任务
        /// </summary>
        /// <returns></returns>
        public void SequentialStart()
        {
            var runningCount = UploadCollection.Count(i => i.Running);

            // 已达最大上传数，退出
            if (runningCount >= MaxUploadCount) return;

            // 剩余上传数
            var leftCount = MaxUploadCount - runningCount;

            var uploadItems = UploadCollection.Where(i => !i.IsFinish && !i.Running).Take(leftCount);

            foreach (var item in uploadItems)
            {
                item.Start();
            }
        }
    }
}