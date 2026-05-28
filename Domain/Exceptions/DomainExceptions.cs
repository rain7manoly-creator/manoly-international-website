namespace ManolyWarehouse.Domain.Exceptions;

/// <summary>
/// Base class for business rule violations.
/// Caller is expected to translate the message to the user (already in Arabic).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class ShelfFullException : DomainException
{
    public ShelfFullException(string shelfCode)
        : base($"الرف {shelfCode} ممتلئ. لا يمكن إضافة عناصر إضافية.") { }
}

public class PositionOccupiedException : DomainException
{
    public PositionOccupiedException(string shelfCode, int position)
        : base($"الموضع {position} على الرف {shelfCode} مشغول بالفعل.") { }
}

public class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(string current, string requested)
        : base($"لا يمكن الانتقال من {current} إلى {requested}.") { }
}

public class TerminalStateException : DomainException
{
    public TerminalStateException()
        : base("هذه الطلبية مغلقة ولا يمكن تعديلها.") { }
}

public class ProductInUseException : DomainException
{
    public ProductInUseException(int shelfCount, int areaZCount)
        : base($"هذا المنتج موجود في {shelfCount} موقع على الرفوف و{areaZCount} موقع في منطقة Z.") { }
}

public class CategoryInUseException : DomainException
{
    public CategoryInUseException(int productCount)
        : base($"لا يمكن حذف هذا التصنيف، يحتوي على {productCount} منتج.") { }
}

public class AreaZDuplicateException : DomainException
{
    public AreaZDuplicateException()
        : base("هذا المنتج موجود بالفعل في منطقة Z.") { }
}
