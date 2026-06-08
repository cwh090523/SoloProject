using System;
using System.Collections.Generic;

public enum BTStatus
{
    Success,
    Failure,
    Running
}

public abstract class BTNode
{
    public abstract BTStatus Tick();
}

public sealed class BTSelector : BTNode
{
    private readonly List<BTNode> _children;

    public BTSelector(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    public override BTStatus Tick()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            BTStatus status = _children[i].Tick();
            if (status != BTStatus.Failure)
                return status;
        }

        return BTStatus.Failure;
    }
}

public sealed class BTSequence : BTNode
{
    private readonly List<BTNode> _children;

    public BTSequence(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    public override BTStatus Tick()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            BTStatus status = _children[i].Tick();
            if (status != BTStatus.Success)
                return status;
        }

        return BTStatus.Success;
    }
}

public sealed class BTCondition : BTNode
{
    private readonly Func<bool> _condition;

    public BTCondition(Func<bool> condition)
    {
        _condition = condition;
    }

    public override BTStatus Tick()
    {
        return _condition != null && _condition() ? BTStatus.Success : BTStatus.Failure;
    }
}

public sealed class BTAction : BTNode
{
    private readonly Func<BTStatus> _action;

    public BTAction(Func<BTStatus> action)
    {
        _action = action;
    }

    public override BTStatus Tick()
    {
        return _action != null ? _action() : BTStatus.Failure;
    }
}
