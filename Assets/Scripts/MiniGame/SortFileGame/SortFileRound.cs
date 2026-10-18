using System;
using System.Collections.Generic;

namespace TapTapGameJam.UI
{
    public enum SortFileDepartment
    {
        HumanResources = 0,
        Administration = 1,
        Technical = 2,
        Design = 3
    }

    // 分拣规则独立于 UI，顺序中的第一个未完成文件就是栈顶。
    public sealed class SortFileRound
    {
        private readonly SortFileDepartment[] _files;
        private readonly int[] _sortedCounts = new int[4];
        private bool _running = true;
        public int SortedCount { get; private set; }
        public int TotalCount => _files.Length;
        public bool IsComplete => SortedCount == TotalCount;

        public SortFileRound(IReadOnlyList<SortFileDepartment> files)
        {
            if (files == null || files.Count == 0)
                throw new ArgumentException("至少需要一份文件。", nameof(files));
            _files = new SortFileDepartment[files.Count];
            for (int i = 0; i < files.Count; i++)
            {
                if ((int)files[i] < 0 || (int)files[i] >= 4)
                    throw new ArgumentOutOfRangeException(nameof(files));
                _files[i] = files[i];
            }
        }

        public SortFileDepartment DepartmentAt(int index) => _files[index];
        public int CountFor(SortFileDepartment department) => _sortedCounts[(int)department];
        public bool CanDrag(int index) => _running && !IsComplete && index == SortedCount;

        public bool TrySort(int index, SortFileDepartment target)
        {
            if (!CanDrag(index) || _files[index] != target) return false;
            _sortedCounts[(int)target]++;
            SortedCount++;
            return true;
        }

        public void Stop() => _running = false;

        public static SortFileDepartment[] CreateBalanced(int filesPerDepartment, Random random = null)
        {
            if (filesPerDepartment < 1)
                throw new ArgumentOutOfRangeException(nameof(filesPerDepartment));
            var files = new SortFileDepartment[checked(filesPerDepartment * 4)];
            for (int i = 0; i < files.Length; i++) files[i] = (SortFileDepartment)(i / filesPerDepartment);
            if (random != null)
            {
                for (int i = files.Length - 1; i > 0; i--)
                {
                    int other = random.Next(i + 1);
                    SortFileDepartment value = files[i];
                    files[i] = files[other];
                    files[other] = value;
                }
            }
            return files;
        }

        public static string DepartmentName(SortFileDepartment department)
        {
            switch (department)
            {
                case SortFileDepartment.HumanResources: return "人事";
                case SortFileDepartment.Administration: return "行政";
                case SortFileDepartment.Technical: return "技术";
                case SortFileDepartment.Design: return "设计";
                default: return string.Empty;
            }
        }
    }
}
