interface ISetTagsData {
	id: number;
	tagIds: number[];
}

interface ITag {
	tagId: number;
	tagName: string;
	tagCategoryId: number;
}

interface ITagCategory {
	tagCategoryId: number;
	tagCategoryName: string;
}