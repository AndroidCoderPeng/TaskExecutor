using GongSolutions.Wpf.DragDrop;
using TaskExecutor.Model;

namespace TaskExecutor.Utils;

/// <summary>
/// 动作库拖出源：锁定（渠道未解锁）或已加入执行序列的动作不允许拖。
/// </summary>
public sealed class SequenceDragHandler : DefaultDragHandler
{
    public override bool CanStartDrag(IDragInfo dragInfo)
        => dragInfo.SourceItem is TaskAction { IsEnabled: true, InSequence: false };
}