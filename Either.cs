using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniLISP
{
    public enum EitherKind
    {
        Invalid,
        Left,
        Right
    }

    public struct Either<T1, T2>
    {
        public readonly EitherKind Which;
        readonly T1 _left;
        readonly T2 _right;

        public T1 Left { get { if (Which != EitherKind.Left) throw new InvalidOperationException(); return _left; } }
        public T2 Right { get { if (Which != EitherKind.Right) throw new InvalidOperationException(); return _right; } }

        public bool IsLeft { get { return Which == EitherKind.Left; } }
        public bool IsRight { get { return Which == EitherKind.Right; } }

        public U Collapse<U>(Func<T1, U> left, Func<T2, U> right)
        {
            if (Which == EitherKind.Left) return left(_left);
            else if (Which == EitherKind.Right) return right(_right);
            else throw new InvalidOperationException();
        }

        public Either(T1 left)
        {
            Which = EitherKind.Left;
            _left = left;
            _right = default(T2);
        }

        public Either(T2 right)
        {
            Which = EitherKind.Right;
            _left = default(T1);
            _right = right;
        }

        public static implicit operator Either<T1, T2>(T1 left)
        {
            return new Either<T1, T2>(left);
        }

        public static implicit operator Either<T1, T2>(T2 right)
        {
            return new Either<T1, T2>(right);
        }
    }
}
