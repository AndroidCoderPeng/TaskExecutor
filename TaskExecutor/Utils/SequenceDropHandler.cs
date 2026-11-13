using System.Collections;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using TaskExecutor.Enums;
using TaskExecutor.Model;
using TaskExecutor.Services;

namespace TaskExecutor.Utils;

/// <summary>
/// 执行序列放置处理：同列表拖拽重排走基类；跨列表（动作库 → 执行序列）插入副本，不动源集合。
/// </summary>
public sealed class SequenceDropHandler : DefaultDropHandler
{
    private readonly IToastService _toastService;

    public SequenceDropHandler(IToastService toastService) => _toastService = toastService;

    /// <summary>
    /// 悬停时决定能否落：只接受解锁的动作，锁定项不显示插入线。
    /// </summary>
    public override void DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.Data is not TaskAction { IsEnabled: true })
        {
            dropInfo.Effects = DragDropEffects.None;
            return;
        }

        base.DragOver(dropInfo);
    }

    public override void Drop(IDropInfo dropInfo)
    {
        if (dropInfo.Data is not TaskAction item) return;

        var target = dropInfo.TargetCollection as IList;
        var source = dropInfo.DragInfo.SourceCollection as IList;

        // 同列表重排：交给基类，自动在插入点移动条目
        if (target != null && ReferenceEquals(source, target))
        {
            base.Drop(dropInfo);
            return;
        }

        // 跨列表：动作库 → 执行序列，插入副本，绝不触碰源集合
        if (target == null) return;

        if (target.OfType<TaskAction>().Any(a => a.Name == item.Name))
        {
            _toastService.Show($"动作「{item.Name}」已在执行序列中", ToastType.Warning);
            return;
        }

        var copy = new TaskAction
        {
            Name = item.Name,
            Color = item.Color,
            DimColor = item.DimColor,
            IsEnabled = true
        };
        target.Insert(Math.Min(dropInfo.InsertIndex, target.Count), copy);

        item.InSequence = true; // 动作库原条目置灰
    }
}