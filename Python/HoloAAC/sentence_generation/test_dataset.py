import unittest

from .dataset import SentenceRetrieval


class MyTestCase(unittest.TestCase):

    def __init__(self, methodName: str = ...) -> None:
        super().__init__(methodName)
        self.sr = None
        self.objects = None
        self.sentences = None
        self.keywords = None
        self.x2 = None
        self.xn = None

    def setUp(self):
        super().setUp()
        self.sr = SentenceRetrieval()
        self.sr.prepare()
        self.objects = self.sr.k_objects
        self.sentences = self.sr.k_sentences
        self.keywords = self.sr.k_keywords
        self.x2 = self.sr.k_x2
        self.xn = self.sr.k_xn

    def check_result(self, result, key):
        self.assertIn(key, result)
        self.assertIn(self.objects, result[key])
        self.assertIn(self.sentences, result[key])
        self.assertIn(self.keywords, result[key])
        self.assertTrue(isinstance(result[key][self.keywords], list))
        self.assertTrue(isinstance(result[key][self.sentences], list))

    def test_na_obj_no_keywords(self):
        result = self.sr.get_sentence_by_objects(objs=None, keywords=[])
        print(result)
        self.check_result(result, '')

    def test_one_obj_no_keywords(self):
        obj = 'water'
        result = self.sr.get_sentence_by_objects(objs=obj, keywords=[])
        print(result)
        self.check_result(result, obj)

    def test_one_obj_one_keyword(self):
        obj = 'water'
        keyword = 'bag'
        result = self.sr.get_sentence_by_objects(objs=obj, keywords=[keyword])
        print(result)
        self.check_result(result, obj)

    def test_one_obj_two_keywords(self):
        obj = 'water'
        keywords = ['bag', 'double']
        result = self.sr.get_sentence_by_objects(objs=obj, keywords=keywords)
        print(result)
        self.check_result(result, obj)

    def test_two_obj_no_keywords(self):
        objs = ['water', 'milk']
        result = self.sr.get_sentence_by_objects(objs=objs)
        print(result)
        self.check_result(result, self.x2)

    def test_two_obj_one_keyword(self):
        objs = ['water', 'milk']
        keyword = 'bag'
        result = self.sr.get_sentence_by_objects(objs=objs, keywords=[keyword])
        print(result)
        self.check_result(result, self.x2)

    def test_three_obj_no_keyword(self):
        objs = ['water', 'milk', 'cereal']
        result = self.sr.get_sentence_by_objects(objs=objs)
        print(result)
        self.check_result(result, self.xn)

    def test_three_obj_one_keyword(self):
        objs = ['water', 'milk', 'cereal']
        keyword = 'bag'
        result = self.sr.get_sentence_by_objects(objs=objs, keywords=[keyword])
        print(result)
        self.check_result(result, self.xn)

    def test_obj_percent(self):
        obj = 'milk'
        keyword = '1%'
        result = self.sr.get_sentence_by_objects(objs=obj, keywords=[keyword])
        print(result)
        self.check_result(result, obj)


if __name__ == '__main__':
    unittest.main()
