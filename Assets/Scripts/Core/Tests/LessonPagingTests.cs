using Museum.Core;
using NUnit.Framework;

namespace Museum.Core.Tests
{
    public class LessonPagingTests
    {
        [Test]
        public void Flatten_ExpandsEachSectionIntoItsPages()
        {
            LessonPage[] pages = LessonPaging.Flatten(new[] { 2, 1, 3 });

            Assert.AreEqual(6, pages.Length);
            Assert.AreEqual(0, pages[0].Section);
            Assert.AreEqual(1, pages[0].PageInSection);
            Assert.AreEqual(0, pages[1].Section);
            Assert.AreEqual(2, pages[1].PageInSection);
            Assert.AreEqual(1, pages[2].Section);
            Assert.AreEqual(1, pages[2].PageInSection);
            Assert.AreEqual(2, pages[5].Section);
            Assert.AreEqual(3, pages[5].PageInSection);
        }

        [Test]
        public void Flatten_SkipsSectionsWithNoPages()
        {
            LessonPage[] pages = LessonPaging.Flatten(new[] { 1, 0, 1 });

            Assert.AreEqual(2, pages.Length);
            Assert.AreEqual(0, pages[0].Section);
            Assert.AreEqual(2, pages[1].Section);
        }

        [Test]
        public void Flatten_HandlesEmptyAndNullInput()
        {
            Assert.IsEmpty(LessonPaging.Flatten(new int[0]));
            Assert.IsEmpty(LessonPaging.Flatten(null));
        }

        [Test]
        public void Wrap_LeavesInRangeIndicesAlone()
        {
            Assert.AreEqual(0, LessonPaging.Wrap(0, 6));
            Assert.AreEqual(3, LessonPaging.Wrap(3, 6));
            Assert.AreEqual(5, LessonPaging.Wrap(5, 6));
        }

        [Test]
        public void Wrap_StepsPastTheEndBackToTheStart()
        {
            Assert.AreEqual(0, LessonPaging.Wrap(6, 6));
            Assert.AreEqual(1, LessonPaging.Wrap(7, 6));
        }

        [Test]
        public void Wrap_StepsBackFromTheFirstPageToTheLast()
        {
            Assert.AreEqual(5, LessonPaging.Wrap(-1, 6));
            Assert.AreEqual(4, LessonPaging.Wrap(-2, 6));
        }

        [Test]
        public void Wrap_IsSafeWhenThereAreNoPages()
        {
            Assert.AreEqual(0, LessonPaging.Wrap(-1, 0));
            Assert.AreEqual(0, LessonPaging.Wrap(3, 0));
        }
    }
}
