type Comparator<Type> = (currData: Type, objData: Type) => boolean;

type Action<Type> = (data: Type) => void;

type Converter<Type, Type2> = (data: Type) => Type2;

type CheckFunc<Type> = (data: Type) => boolean;

type ProgressionFunc = (percentComplete: number) => number

type SlotVariant = "equipped" | "inventory";

enum ChangeType {
    Edit = 0,
    Add = 1,
    Delete = 2
}