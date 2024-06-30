interface ISetTagsData {
	id: number;
	tagIds: number[];
}

interface ITag {
	id: number;
	name: string;
	tagCategoryId: number;
}

interface ITagCategory {
	id: number;
	name: string;
}