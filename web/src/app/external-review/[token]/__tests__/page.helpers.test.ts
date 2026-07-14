import { describe, it, expect } from "vitest";
import { isSectionEditable, filterEditableSections } from "../page";

describe("isSectionEditable", () => {
  it("treats every section as editable when editableSectionIndices is null (backward compat)", () => {
    expect(isSectionEditable(0, null)).toBe(true);
    expect(isSectionEditable(5, null)).toBe(true);
  });

  it("treats every section as editable when editableSectionIndices is undefined", () => {
    expect(isSectionEditable(0, undefined)).toBe(true);
  });

  it("treats a section as editable only if its index is in a non-null editableSectionIndices", () => {
    expect(isSectionEditable(1, [0, 1])).toBe(true);
    expect(isSectionEditable(2, [0, 1])).toBe(false);
  });
});

describe("filterEditableSections", () => {
  it("includes every edited section when editableSectionIndices is null", () => {
    const result = filterEditableSections({ 0: "a", 1: "b", 2: "c" }, null);
    expect(result).toEqual(
      expect.arrayContaining([
        { sectionIndex: 0, translatedText: "a" },
        { sectionIndex: 1, translatedText: "b" },
        { sectionIndex: 2, translatedText: "c" },
      ])
    );
    expect(result).toHaveLength(3);
  });

  it("excludes edits for sections outside the editable set before submission", () => {
    const result = filterEditableSections({ 0: "a", 1: "b", 2: "c" }, [0, 2]);
    expect(result).toEqual(
      expect.arrayContaining([
        { sectionIndex: 0, translatedText: "a" },
        { sectionIndex: 2, translatedText: "c" },
      ])
    );
    expect(result).toHaveLength(2);
  });

  it("returns an empty array when no edited sections are in the editable set", () => {
    const result = filterEditableSections({ 3: "d" }, [0, 1]);
    expect(result).toEqual([]);
  });
});
