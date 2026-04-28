//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq.Expressions;
//using UnityEngine;
//using UnityEngine.UI;

//public class TimeLineManager : MonoBehaviour
//{
//    public class Point
//    {
//        public int ID;
//        protected GameObject target;
//        protected string targetName;
//        protected Vector3 vector;
//        protected long start;
//        protected long duration;
//        protected long end;
//        public long Start { get => start; }
//        public long Duration { get => duration; }
//        public long End { get => end; }

//        public enum MovementType
//        {
//            Vector,
//            To,
//        }
//        protected MovementType movementType;
//        public enum OriginValueType
//        {
//            Current,
//            WhenStart,
//            Set
//        }
//        protected OriginValueType originValueType;
//        protected Vector3 OriginValue;
//        protected AnimationCurve Curve = null;

//        public Point(int id, GameObject obj, Vector3 vec, long st, long dur, MovementType type, AnimationCurve curve, OriginValueType Origintype)
//        {
//            ID = id;
//            target = obj;
//            targetName = target.name;
//            vector = vec;
//            start = st;
//            duration = dur;
//            end = start + duration;
//            movementType = type;
//            Curve = curve;
//            originValueType = Origintype;
//        }
//        public float CalculValue(long timer, float start, float end)
//        {
//            long F0 = this.start;
//            long F1 = this.end;
//            float rate = (float)(timer - F0) / (F1 - F0);
//            return start + (end - start) * Curve.Evaluate(rate);
//        }
//        public virtual void ExecuteNode(long timer)
//        {
            
//        }
//        public virtual void SetOriginValue(Vector3 _vector)
//        {
//            switch (originValueType)
//            {
//                case OriginValueType.Set:
//                    OriginValue = _vector;
//                    break;
//            }
//        }
//        protected void RemoveThisNode()
//        {
//            end = start - 1;
//        }
//        protected bool isNodeTargetAvailable()
//        {
//            if (target == null)
//            {
//                RemoveThisNode();
//                Debug.Log("node remove :" + targetName);
//                return false;
//            }
//            else
//            {
//                return true;
//            }
//        }
//    }
//    public class Point_position : Point
//    {
//        public Point_position(int id, GameObject obj, Vector3 vec, long st, long dur, MovementType type, AnimationCurve curve, OriginValueType Origintype) : base(id, obj, vec, st, dur, type, curve, Origintype)
//        {
//            switch (originValueType)
//            {
//                case OriginValueType.Current:
//                    OriginValue = target.transform.localPosition;
//                    break;
//            }
//        }
//        public override void ExecuteNode(long timer)
//        {
//            if (!isNodeTargetAvailable()) { return; }
//            if (timer == start)
//            {
//                switch (originValueType)
//                {
//                    case OriginValueType.WhenStart:
//                        try
//                        {
//                            OriginValue = target.transform.localPosition;
//                        }
//                        catch
//                        {
//                            Debug.Log(targetName);
//                        }
//                        OriginValue = target.transform.localPosition;
//                        break;
//                }
//            }
//            switch (movementType)
//            {
//                case MovementType.Vector:
//                    target.transform.localPosition = target.transform.localPosition + new Vector3(CalculValue(timer, 0, vector.x) - CalculValue(timer - 1, 0, vector.x), CalculValue(timer, 0, vector.y) - CalculValue(timer - 1, 0, vector.y), CalculValue(timer, 0, vector.z) - CalculValue(timer - 1, 0, vector.z));
//                    break;
//                case MovementType.To:
//                    target.transform.localPosition = new Vector3(CalculValue(timer, OriginValue.x, vector.x), CalculValue(timer, OriginValue.y, vector.y), CalculValue(timer, OriginValue.z, vector.z));
//                    // Debug.Log(new Vector3(CalculValue(timer, OriginValue.x, vector.x), CalculValue(timer, OriginValue.y, vector.y), CalculValue(timer, OriginValue.z, vector.z)));
//                    break;
//            }
//        }
//    }
//    public class Point_rectPosition : Point
//    {
//        public Point_rectPosition(int id, GameObject obj, Vector3 vec, long st, long dur, MovementType type, AnimationCurve curve, OriginValueType Origintype) : base(id, obj, vec, st, dur, type, curve, Origintype)
//        {
//            switch (originValueType)
//            {
//                case OriginValueType.Current:
//                    OriginValue = target.GetComponent<RectTransform>().anchoredPosition;
//                    break;
//            }
//        }
//        public override void ExecuteNode(long timer)
//        {
//            if (!isNodeTargetAvailable()) { return; }
//            if (timer == start)
//            {
//                switch (originValueType)
//                {
//                    case OriginValueType.WhenStart:
//                        OriginValue = target.GetComponent<RectTransform>().anchoredPosition;
//                        break;
//                }
//            }
//            switch (movementType)
//            {
//                case MovementType.Vector:
//                    target.GetComponent<RectTransform>().anchoredPosition = target.GetComponent<RectTransform>().anchoredPosition + new Vector2(CalculValue(timer, 0, vector.x) - CalculValue(timer - 1, 0, vector.x), CalculValue(timer, 0, vector.y) - CalculValue(timer - 1, 0, vector.y));
//                    break;
//                case MovementType.To:
//                    target.GetComponent<RectTransform>().anchoredPosition = new Vector2(CalculValue(timer, OriginValue.x, vector.x), CalculValue(timer, OriginValue.y, vector.y));
//                    break;
//            }
//        }
//    }
//    public class Point_scale : Point
//    {
//        public Point_scale(int id, GameObject obj, Vector3 vec, long st, long dur, MovementType type, AnimationCurve curve, OriginValueType Origintype) : base(id, obj, vec, st, dur, type, curve, Origintype)
//        {
//            switch (originValueType)
//            {
//                case OriginValueType.Current:
//                    OriginValue = target.transform.localScale;
//                    break;
//            }
//        }
//        public override void ExecuteNode(long timer)
//        {
//            if (!isNodeTargetAvailable()) { return; }
//            if (timer == start)
//            {
//                switch (originValueType)
//                {
//                    case OriginValueType.WhenStart:
//                        OriginValue = target.transform.localScale;
//                        break;
//                }
//            }
//            switch (movementType)
//            {
//                case MovementType.Vector:
//                    target.transform.localScale = target.transform.localScale + new Vector3(CalculValue(timer, 0, vector.x) - CalculValue(timer - 1, 0, vector.x), CalculValue(timer, 0, vector.y) - CalculValue(timer - 1, 0, vector.y), CalculValue(timer, 0, vector.z) - CalculValue(timer - 1, 0, vector.z));
//                    break;
//                case MovementType.To:
//                    target.transform.localScale = new Vector3(CalculValue(timer, OriginValue.x, vector.x), CalculValue(timer, OriginValue.y, vector.y), CalculValue(timer, OriginValue.z, vector.z));
//                    break;
//            }
//        }
//    }
//    public class Point_rotation : Point
//    {
//        public Point_rotation(int id, GameObject obj, Vector3 vec, long st, long dur, MovementType type, AnimationCurve curve, OriginValueType Origintype) : base(id, obj, vec, st, dur, type, curve, Origintype)
//        {
//            switch (originValueType)
//            {
//                case OriginValueType.Current:
//                    OriginValue = target.transform.localRotation.eulerAngles;
//                    break;
//            }
//        }
//        public override void ExecuteNode(long timer)
//        {
//            if (!isNodeTargetAvailable()) { return; }
//            if (timer == start)
//            {
//                switch (originValueType)
//                {
//                    case OriginValueType.WhenStart:
//                        OriginValue = target.transform.localRotation.eulerAngles;
//                        break;
//                }
//            }
//            switch (movementType)
//            {
//                case MovementType.Vector:
//                    target.transform.localRotation = Quaternion.Euler(target.transform.localRotation.eulerAngles + new Vector3(CalculValue(timer, 0, vector.x) - CalculValue(timer - 1, 0, vector.x), CalculValue(timer, 0, vector.y) - CalculValue(timer - 1, 0, vector.y), CalculValue(timer, 0, vector.z) - CalculValue(timer - 1, 0, vector.z)));
//                    break;
//                case MovementType.To:
//                    target.transform.localRotation = Quaternion.Euler(new Vector3(CalculValue(timer, OriginValue.x, vector.x), CalculValue(timer, OriginValue.y, vector.y), CalculValue(timer, OriginValue.z, vector.z)));
//                    break;
//            }
//        }
//    }
//    public class Point_SpriteOpacity : Point
//    {
//        protected float OriginValue;
//        public Point_SpriteOpacity(int id, GameObject obj, Vector3 vec, long st, long dur, MovementType type, AnimationCurve curve, OriginValueType Origintype) : base(id, obj, vec, st, dur, type, curve, Origintype)
//        {
//            switch (originValueType)
//            {
//                case OriginValueType.Current:
//                    OriginValue = target.GetComponent<SpriteRenderer>().color.a;
//                    break;
//            }
//        }
//        public override void ExecuteNode(long timer)
//        {
//            if (!isNodeTargetAvailable()) { return; }
//            if (timer == start)
//            {
//                switch (originValueType)
//                {
//                    case OriginValueType.WhenStart:
//                        OriginValue = target.GetComponent<SpriteRenderer>().color.a;
//                        break;
//                }
//            }
//            switch (movementType)
//            {
//                case MovementType.Vector:
//                    target.GetComponent<SpriteRenderer>().color = new Color(target.GetComponent<SpriteRenderer>().color.r, target.GetComponent<SpriteRenderer>().color.g, target.GetComponent<SpriteRenderer>().color.b, OriginValue + CalculValue(timer, 0, vector.x));
//                    break;
//                case MovementType.To:
//                    target.GetComponent<SpriteRenderer>().color = new Color(target.GetComponent<SpriteRenderer>().color.r, target.GetComponent<SpriteRenderer>().color.g, target.GetComponent<SpriteRenderer>().color.b, CalculValue(timer, OriginValue, vector.x));
//                    break;
//            }
//        }
//    }
//    public class Point_ImageOpacity : Point
//    {
//        protected float OriginValue;
//        public Point_ImageOpacity(int id, GameObject obj, Vector3 vec, long st, long dur, MovementType type, AnimationCurve curve, OriginValueType Origintype) : base(id, obj, vec, st, dur, type, curve, Origintype)
//        {
//            switch (originValueType)
//            {
//                case OriginValueType.Current:
//                    OriginValue = target.GetComponent<Image>().color.a;
//                    break;
//            }
//        }
//        public override void ExecuteNode(long timer)
//        {
//            if (!isNodeTargetAvailable()) { return; }
//            if (timer == start)
//            {
//                switch (originValueType)
//                {
//                    case OriginValueType.WhenStart:
//                        OriginValue = target.GetComponent<Image>().color.a;
//                        break;
//                }
//            }
//            Color color = new Color(1, 1, 1, 1);
//            switch (movementType)
//            {
//                case MovementType.Vector:
//                {
//                    color = new Color(target.GetComponent<Image>().color.r, target.GetComponent<Image>().color.g, target.GetComponent<Image>().color.b, OriginValue + CalculValue(timer, 0, vector.x));
//                    break;
//                }
//                case MovementType.To:
//                {
//                    color = new Color(target.GetComponent<Image>().color.r, target.GetComponent<Image>().color.g, target.GetComponent<Image>().color.b, CalculValue(timer, OriginValue, vector.x));
//                    break;
//                }

//            }

//            foreach (Image c in target.GetComponentsInChildren<Image>())
//            {
//                c.color = color;
//            }
//            target.GetComponent<Image>().color = color;
//        }
//    }


//    protected LinkedList<Point> TimeLineList = new LinkedList<Point>();
//    protected int ID_ = 0;
//    Queue<LinkedListNode<Point>> RemoveNodeQueue = new Queue<LinkedListNode<Point>>();
//    [SerializeField] protected long timer;
//    public long Timer { get => timer; }
//    [SerializeField] protected AnimationCurve defaultCurve;
//    [SerializeField] protected AnimationCurve zoomIn;
//    [SerializeField] protected AnimationCurve zoomOut;
//    public AnimationCurve DefaultCurve { get => defaultCurve; }
//    public AnimationCurve ZoomIn { get => zoomIn; }
//    public AnimationCurve ZoomOut { get => zoomOut; }
//    //singleton
//    static TimeLineManager instance;
//    public static TimeLineManager Instance { get => instance; }
//    public enum ControllType
//    {
//        position,
//        rectPosition,
//        scale,
//        rotation,
//        spriteOpacity_X,
//        imageOpacity_X
//    }
//    void Awake()
//    {
//        if (instance != null)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        instance = this;
//        DontDestroyOnLoad(instance);
//    }

//    private int EventRegister(ControllType controllType, GameObject obj, Vector3 vec, long st, long dur, Point.MovementType movementType, AnimationCurve curve, Point.OriginValueType originType, Vector3 setVector)
//    {
//        Point node = null;
//        switch (controllType)
//        {
//            case ControllType.position:
//                node = new Point_position(ID_, obj, vec, st, dur, movementType, curve, originType);
//                break;
//            case ControllType.rectPosition:
//                node = new Point_rectPosition(ID_, obj, vec, st, dur, movementType, curve, originType);
//                break;
//            case ControllType.scale:
//                node = new Point_scale(ID_, obj, vec, st, dur, movementType, curve, originType);
//                break;
//            case ControllType.rotation:
//                node = new Point_rotation(ID_, obj, vec, st, dur, movementType, curve, originType);
//                break;
//            case ControllType.spriteOpacity_X:
//                node = new Point_SpriteOpacity(ID_, obj, vec, st, dur, movementType, curve, originType);
//                break;
//            case ControllType.imageOpacity_X:
//                node = new Point_ImageOpacity(ID_, obj, vec, st, dur, movementType, curve, originType);
//                break;
//            default:
//                Debug.LogError("unfinish type:" + controllType);
//                break;
//        }
//        if (originType == Point.OriginValueType.Set)
//        {
//            node.SetOriginValue(setVector);
//        }
//        SetRegister(node);
//        ID_ += 1;
//        return ID_ - 1;
//    }
//    public int EventRegister_To_Current(ControllType type, GameObject obj, Vector3 vec, long st, long dur, AnimationCurve curve)
//    {
//        return EventRegister(type, obj, vec, st, dur, Point.MovementType.To, curve, Point.OriginValueType.Current, new Vector3(0, 0, 0));
//    }
//    public int EventRegister_To_WhenStart(ControllType type, GameObject obj, Vector3 vec, long st, long dur, AnimationCurve curve)
//    {
//        return EventRegister(type, obj, vec, st, dur, Point.MovementType.To, curve, Point.OriginValueType.WhenStart, new Vector3(0, 0, 0));
//    }
//    public int EventRegister_To_Set(ControllType type, GameObject obj, Vector3 vec, long st, long dur, AnimationCurve curve, Vector3 setVector)
//    {
//        return EventRegister(type, obj, vec, st, dur, Point.MovementType.To, curve, Point.OriginValueType.Set, setVector);
//    }
//    public int EventRegister_Vector(ControllType type, GameObject obj, Vector3 vec, long st, long dur, AnimationCurve curve)
//    {
//        return EventRegister(type, obj, vec, st, dur, Point.MovementType.Vector, curve, Point.OriginValueType.Current, new Vector3(0, 0, 0));
//    }


//    public void SetRegister(Point node)
//    {
//        TimeLineList.AddLast(new LinkedListNode<Point>(node));
//        return;
//    }

//    public bool IsActionActive(int id)
//    {
//        foreach (var node in TimeLineList)
//        {
//            if (node.ID == id)
//            {
//                return true;
//            }
//        }
//        return false;
//    }
//    void Update()
//    {
//        int nodeCount = TimeLineList.Count;
//        if (TimeLineList.Count > 0)
//        {
//            LinkedListNode<Point> node = TimeLineList.First;
//            for (int i = 0; i < nodeCount; i++)
//            {
//                if (timer > node.Value.End)
//                {
//                    RemoveNodeQueue.Enqueue(node);
//                }
//                else if (timer <= node.Value.End && timer >= node.Value.Start)
//                {
//                    node.Value.ExecuteNode(timer);
//                }
//                node = node.Next;
//            }
//            while (RemoveNodeQueue.Count > 0)
//            {
//                try
//                {
//                    TimeLineList.Remove(RemoveNodeQueue.Dequeue());
//                }
//                catch
//                {
//                    //just ignore multiple deletion
//                }
//            }
//        }
//        timer++;
//    }
//    public void RemoveNode(int nodeID)
//    {
//        int nodeCount = TimeLineList.Count;
//        LinkedListNode<Point> node = TimeLineList.First;
//        for (int i = 0; i < nodeCount; i++)
//        {
//            if (node.Value.ID == nodeID)
//            {
//                RemoveNodeQueue.Enqueue(node);
//                break;
//            }
//        }
//    }
//}
