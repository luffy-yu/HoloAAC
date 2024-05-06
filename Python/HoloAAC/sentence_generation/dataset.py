import json
import os
import re
from itertools import product

import funcy
import nltk
import pandas as pd
import yaml
from nltk import word_tokenize
from nltk.corpus import stopwords
from nltk.corpus import wordnet
from nltk.stem import WordNetLemmatizer
from sklearn.feature_extraction.text import CountVectorizer
from sklearn.feature_extraction.text import TfidfTransformer
from yaml import Loader

from copy import deepcopy

CUR_DIR = os.path.dirname((os.path.abspath(__file__)))


class Document(object):
    __doc__ = "Yaml document"

    def __init__(self, path):
        self.path = os.path.join(CUR_DIR, path)
        self.data = {}

    @staticmethod
    def parse_yaml_file(filename):
        if not os.path.exists(filename):
            return {}
        return yaml.load(open(filename, 'r'), Loader=Loader)

    def parse(self):
        self.data = Document.parse_yaml_file(self.path)
        self.dfs_parse_include()

    def parse_include(self):
        key = 'include'
        if key in self.data:
            filename = self.data[key]
            data = Document.parse_yaml_file(filename)
            # override the included data
            # remove 'include' in self.data
            del self.data[key]
            data.update(self.data)
            self.data = data

    def dfs_parse_include(self):

        def dfs(data, cur_path):
            key = 'include'
            if key not in data:
                return data
            filepath = os.path.join(cur_path, data[key])
            # include data
            include_data = Document.parse_yaml_file(filepath)
            # remove key in data
            del data[key]
            result = dfs(include_data, filepath)
            result.update(data)
            return result

        self.data = dfs(self.data, self.path)


class Common(Document):

    @property
    def topics(self):
        return self.data.get('topics')

    def get_topic(self, name):
        return self.topics.get(name)

    def get_sentences(self, name):
        return self.topics.get(name, {}).get('sentences', [])


# base class of milk, water, and etc.

class ObjectBase(Common):

    @property
    def topics(self):
        key = self.data.get('what')
        return self.data.get(key, {}).get('topics', [])

    @property
    def what(self):
        key = self.data.get('what')
        return self.data.get(key, {}) if key else {}

    @property
    def name(self):
        return self.what.get('name')

    @property
    def names(self):
        return self.what.get('names')

    @property
    def adjectives(self):
        return self.what.get('adjectives')

    @property
    def what_topics(self):
        return self.what.get('topics')

    def get_sentences(self, name):
        return self.data.get('topics').get(name, {}).get('sentences', [])

    @property
    def sentences(self):
        # self sentences
        self_sentences = self.what.get('sentences')
        # format
        self_sentences = funcy.merge(*[self.format_sentence(sts, self.name, self.names, self.adjectives)
                                       for sts in self_sentences]) if self_sentences else []
        # sentence from topics
        topics_sentences = funcy.merge(*[self.get_sentences(key) for key in self.topics])
        # format
        topics_sentences = funcy.merge(*[self.format_sentence(sts, self.name, self.names, self.adjectives)
                                         for sts in topics_sentences]) if topics_sentences else []

        sentences = self_sentences + topics_sentences
        # sentences of topics
        return sentences

    @staticmethod
    def format_sentence(sentence, name, names, adjs):
        name = [name] if name else []
        names = [names] if names else []
        if adjs:
            name = product(adjs, name)
            name = list(map(lambda x: ' '.join(x), name))

            names = product(adjs, names)
            names = list(map(lambda x: ' '.join(x), names))

        # it's fine to use  '{name}'.format(name=1, names=2),
        # while it raise a KeyError to use '{name}'.format(name=1, names=2)

        result = []
        for n, ns in zip(name, names):
            result.append(sentence.format(name=n, names=ns))

        return result


# special cases of x2 and xn

class X2(ObjectBase):

    def __init__(self, path):
        super(X2, self).__init__(path)
        self._one = ''
        self._two = ''

    @property
    def one(self):
        return self._one

    @one.setter
    def one(self, v):
        self._one = v

    @property
    def two(self):
        return self._two

    @two.setter
    def two(self, v):
        self._two = v

    @property
    def sentences(self):
        # self sentences
        self_sentences = self.what.get('sentences')

        if self.one and self.two:
            # format only when one and two are set
            self_sentences = [self.format(sentence, self.one, self.two) for sentence in self_sentences]

        return self_sentences

    @staticmethod
    def format(sentence, one, two):
        return sentence.format(one=one, two=two)


class XN(ObjectBase):

    @property
    def sentences(self):
        return self.what.get('sentences')

    @property
    def name(self):
        return 'groceries'

    @property
    def names(self):
        return 'groceries'


class Manager(Document):
    __doc__ = "Dataset manager"

    config = 'dataset/__config__.yml'

    def __init__(self):
        super(Manager, self).__init__(self.config)
        self.root_path = os.path.join(CUR_DIR, 'dataset/')
        self._dataset = {}

        # collect all name, all names to filter keywords
        self.all_name = {}
        self.all_names = {}

        self.parser_cls_map = {
            'default': ObjectBase,
            'x2': X2,
            'xn': XN,
        }

    @property
    def enabled(self):
        return self.data.get('enabled') or []

    @property
    def extension(self):
        return self.data.get('extension') or []

    @property
    def dataset(self):
        return self._dataset

    @dataset.setter
    def dataset(self, v):
        self._dataset = v

    def load_managees(self):

        for obj in self.enabled + self.extension:
            filename = os.path.join(self.root_path, f'{obj}.yml')
            if not os.path.exists(filename):
                continue
            parser_cls = self.parser_cls_map.get(obj if obj in self.parser_cls_map else 'default')
            ob = parser_cls(filename)
            ob.parse()
            # update all_name, and all_names
            # fix object name like tomato_sauce
            name = obj.replace('_', ' ')
            self.all_name[name] = ob.name
            self.all_names[name] = ob.names

            self._dataset[name] = ob.sentences

    def get_name_names(self, obj):
        # fix combined object, like `tomato sauce`
        return self.all_name[obj].split(' ') + self.all_names[obj].split(' ')

    def show(self):
        print(json.dumps(self._dataset, indent=2, ensure_ascii=False))

    def format_x2(self, one, two):
        sentences = []
        key = 'x2'
        if key in self.dataset:
            sentences = self.dataset[key]
            sentences = [X2.format(sentence, one, two) for sentence in sentences]
        return sentences

    @property
    def x2_sentences(self):
        return self._dataset['x2']

    @property
    def xn_sentences(self):
        return self._dataset['xn']


class NLPProcessor(object):

    def __init__(self):
        self.lemmatizer = WordNetLemmatizer()
        # excluded stopwords
        self.exclude_stopwords = {'have'}
        self.stopwords = set(stopwords.words('english')) - self.exclude_stopwords
        self.punctuations = "?:!.,;"
        self.count_vectorizer = CountVectorizer(token_pattern='(?u)\\b\\w+\\b')

        self.punctuations_pattern = re.compile(r"[?:!.,;]")

        # fix keyword in milk, like 1%, 2%
        self.keywords_map = {
            '1': '1%',
            '2': '2%',
        }

    @staticmethod
    def get_wordnet_pos(word):
        """Map POS tag to first character lemmatize() accepts"""
        tag = nltk.pos_tag([word])[0][1][0].upper()
        tag_dict = {"J": wordnet.ADJ,
                    "N": wordnet.NOUN,
                    "V": wordnet.VERB,
                    "R": wordnet.ADV}

        return tag_dict.get(tag, wordnet.NOUN)

    def stem_sentence(self, sentence):
        # tokenize
        tokens = word_tokenize(sentence)
        # remove punctuations
        tokens = list(filter(lambda x: x not in self.punctuations, tokens))
        # remove stop words, e.g., a, the
        tokens = list(filter(lambda x: x.lower() not in self.stopwords, tokens))

        """This might be unnecessary."""
        # # lemmatize
        # tokens = list(map(lambda x: self.lemmatizer.lemmatize(x, self.get_wordnet_pos(x)), tokens))

        # join
        sentence = ' '.join(tokens)
        return sentence

    def get_n_keywords(self, sentences, n, exclude_keywords=[]):
        sentences = [self.stem_sentence(sentence) for sentence in sentences]
        word_count_vector = self.count_vectorizer.fit_transform(sentences)
        tfidf_transformer = TfidfTransformer(smooth_idf=True, use_idf=True)
        tfidf_transformer.fit(word_count_vector)
        df_idf = pd.DataFrame(tfidf_transformer.idf_,
                              index=self.count_vectorizer.get_feature_names(), columns=["idf_weights"])
        df_idf = df_idf.sort_values(by=['idf_weights'])
        keywords = [keyword for keyword in list(df_idf.index) if keyword not in exclude_keywords][: n]
        # fix keywords
        keywords = [self.keywords_map.get(k, k) for k in keywords]
        return keywords

    def preprocess(self, sentences, keywords_count, exclude_keywords=[]):
        """
        Preprocess the sentences,
        @param sentences: [str]
        @param keywords_count: the maximum number of keywords should return
        @param obj_name: object name that should NOT occur in keywords
        @return: {sentences, keywords}
        """
        keywords = self.get_n_keywords(sentences, keywords_count, exclude_keywords=exclude_keywords)
        return dict(sentences=sentences, keywords=keywords)

    def filter_sentence(self, sentences, keywords=[]):
        """
        filter sentence by the keywords, all keywords should be in the sentence
        @param sentences: [str]
        @param keywords: [str]
        @return: [str]
        """
        # set(keywords).difference(s.split()) == empty set
        if not keywords:
            return sentences
        # remove punctuations = list(filter(lambda x: x not in self.punctuations, tokens))
        result = list(filter(lambda x: not set(keywords).difference(
            self.punctuations_pattern.sub('', x.lower()).split()), sentences))
        return result


class SentenceRetrieval(object):

    def __init__(self, keywords_count=10):
        self.keywords_count = keywords_count
        self.manager = Manager()
        self.nlp_processor = NLPProcessor()
        # for requesting sentences without any object
        self.na_object_sentences = {}
        self.frequency_cache = {}  # TODO

        self.key_objects = 'objects'
        self.key_sentences = 'sentences'
        self.key_keywords = 'keywords'

        self.key_x2 = 'x2'
        self.key_xn = 'xn'

        # frequency cache file
        self.frequency_cache_file = 'frequency.json'

        self.init_frequency_cache()

    def init_frequency_cache(self):
        file = os.path.join(CUR_DIR, self.frequency_cache_file)
        if not os.path.exists(file):
            return
        try:
            with open(file, 'r') as f:
                self.frequency_cache = json.load(f)
        except:
            print('Failed to load frequency cache file')

    def update_frequency(self, sentence):
        if sentence in self.frequency_cache:
            self.frequency_cache[sentence] += 1
        else:
            self.frequency_cache.setdefault(sentence, 1)

    def get_sentences_by_frequency(self, sentences, desc=True):
        frequency = {}
        for sentence in sentences:
            frequency[sentence] = self.frequency_cache.get(sentence, 0)

        # sort items
        sorted_items = sorted(frequency.items(), key=lambda x: x[1], reverse=desc)
        sentences = [item[0] for item in sorted_items]
        return sentences

    def prepare(self):
        self.manager.parse()
        self.manager.load_managees()
        # update managed dataset
        dataset = self.manager.dataset
        for key in dataset.keys():
            # ignore extension
            if key in self.manager.extension:
                continue
            data = self.nlp_processor.preprocess(dataset[key], self.keywords_count,
                                                 exclude_keywords=self.manager.get_name_names(key))
            dataset[key] = data

        # update manager
        self.manager.dataset = dataset

        # [TODO]
        # update na_object_sentences
        # only use xn sentences?
        # all_sentences = funcy.merge(*[item[self.key_sentences]
        # for item in dataset.values() if isinstance(item, dict)])
        self.na_object_sentences = self.nlp_processor.preprocess(dataset[self.k_xn],
                                                                 self.keywords_count,
                                                                 exclude_keywords=self.manager.get_name_names(key))

    @property
    def dataset(self):
        return self.manager.dataset

    @property
    def k_objects(self):
        return self.key_objects

    @property
    def k_keywords(self):
        return self.key_keywords

    @property
    def k_sentences(self):
        return self.key_sentences

    @property
    def k_x2(self):
        return self.key_x2

    @property
    def k_xn(self):
        return self.key_xn

    def get_sentence_by_objects(self, objs=None, keywords=[]):
        """
        get sentence by object, or objects, or None

            0) na object:
                return all sentences, exclude extension sentences
            1) single object:
                default showing sentences are sorted in frequency
            2) many objects:
                i) 2 objects: default showing sentences are from x2
                iii) >2 objects: default showing sentences are from xn

        No matter what object(s), the result data structure should be consistent.
        @param objs: str, or [], or None
        @param keywords: str, or [], or None
        @return: {obj: {objects: [], sentences:[], keywords: []}}
        """

        ko = self.k_objects
        ks = self.k_sentences
        kk = self.k_keywords

        k2 = self.key_x2
        kn = self.key_xn

        # filter objs
        if isinstance(objs, list):
            objs = [obj for obj in objs if obj in self.dataset]

        if not objs or (isinstance(objs, str) and objs not in self.dataset):
            result = {ko: ['']}
            dic = deepcopy(self.na_object_sentences)
            # filter sentence by keywords
            dic[ks] = self.nlp_processor.filter_sentence(dic[ks], keywords)
            # sort by frequency
            dic[ks] = self.get_sentences_by_frequency(dic[ks])
            result.update(dic)
            return {'': result}

        if isinstance(objs, str) or (isinstance(objs, list) and len(objs) == 1):
            objs = objs[0] if isinstance(objs, list) else objs
            result = {ko: [objs]}
            dic = deepcopy(self.dataset[objs])
            dic[ks] = self.nlp_processor.filter_sentence(dic[ks], keywords)
            dic[ks] = self.get_sentences_by_frequency(dic[ks])
            result.update(dic)
            return {objs: result}

        size = len(objs)

        if size == 2:
            result = {ko: objs}
            # use x2
            x2_sentences = self.manager.format_x2(objs[0], objs[1])
            #  exclude
            exclude = self.manager.get_name_names(objs[0]) + self.manager.get_name_names(objs[1])
            dic = self.nlp_processor.preprocess(x2_sentences, self.keywords_count,
                                                exclude_keywords=exclude)
            dic[ks] = self.nlp_processor.filter_sentence(dic[ks], keywords)
            dic[ks] = self.get_sentences_by_frequency(dic[ks])
            result.update(dic)
            return {k2: result}

        # size > 2:
        result = {ko: objs}
        sentence = self.manager.xn_sentences
        dic = self.nlp_processor.preprocess(sentence, self.keywords_count,
                                            exclude_keywords=self.manager.get_name_names(self.key_xn))
        dic[ks] = self.nlp_processor.filter_sentence(dic[ks], keywords)
        dic[ks] = self.get_sentences_by_frequency(dic[ks])
        result.update(dic)
        return {kn: result}


# speed up via pre-initialization

sentence_retrieval = SentenceRetrieval()
sentence_retrieval.prepare()

if __name__ == '__main__':
    # ds = Dataset('dataset/base/common.yml')
    # ds.parse()
    # common = Common('dataset/base/common.yml')
    # common.parse()
    #
    # print(common.topics)
    milk = ObjectBase('dataset/milk.yml')
    milk.parse()
    # print(milk.sentences)

    mg = Manager()
    mg.parse()
    mg.load_managees()
    mg.show()

    # nlpp = NLPProcessor()
    # keywords = nlpp.get_n_keywords(milk.sentences, 10)
    # print(keywords)
