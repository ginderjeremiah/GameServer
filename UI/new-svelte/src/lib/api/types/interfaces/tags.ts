export interface ITag {
	id: number;
	name: string;
	tagCategoryId: number;
};

export interface ISetTagsData {
	id: number;
	tagIds: number[];
};

export interface ITagCategory {
	id: number;
	name: string;
};