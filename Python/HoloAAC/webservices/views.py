import os

from django.shortcuts import render

# Create your views here.
from django.http import HttpResponse, FileResponse, JsonResponse, HttpResponseBadRequest
from django.views.decorators.csrf import csrf_exempt
from django.conf import settings

import base64
import json
import os

import uuid

import time
import traceback
from PIL import Image
from io import BytesIO

import numpy as np
import cv2

from object_detection.inference import ObjectDetection
from sentence_generation.dataset import sentence_retrieval
from TTS.tts import TextToSpeech, Voices

from click import secho

"""Global constants"""
KO = sentence_retrieval.k_objects
KK = sentence_retrieval.k_keywords
KS = sentence_retrieval.k_sentences


def cast_voice(voice):
    result = Voices.Male
    for item in Voices:
        if item.name.lower() == voice.lower():
            result = item
            break
    return result


def make_image_filename(filename):
    try:
        folder = settings.OBJECT_DETECTION_IMAGE_FOLDER
        name = f'{os.path.splitext(filename)[0]}.png'
        filename = os.path.join(folder, name)
    except:
        print(traceback.format_exc())
    return filename


def make_mp3_filename(image_name):
    filename = 'temp.mp3'
    try:
        folder = settings.TTS_SOUND_FOLDER
        name = f'{os.path.splitext(os.path.basename(image_name))[0]}.mp3'
        filename = os.path.join(folder, name)
    except:
        print(traceback.format_exc())
    return filename


def get_image_max_side():
    """
    Get number keywords in settings

    @return:
    """
    max_side = 512

    try:
        max_side = settings.IMAGE_MAX_SIDE
    except:
        pass
    return max_side


class ImageHelper(object):

    @staticmethod
    def encode(filename):
        with open(filename, 'rb') as f:
            return base64.b64encode(f.read())

    @staticmethod
    def decode(data, save_to=None, resize=False):
        image = Image.open(BytesIO(base64.b64decode(data)))
        if resize:
            image = ImageHelper.resize(image)
        if save_to is not None:
            image.save(save_to)
        return image

    @staticmethod
    def resize(image):
        width, height = image.size
        max_side = get_image_max_side()
        ratio = max_side / max(width, height)
        width, height = int(width * ratio), int(height * ratio)
        return image.resize((width, height))

    @staticmethod
    def PILImage_to_CVImage(image, cvt_color=False):
        # use numpy to convert the pil_image into a numpy array
        image = np.array(image)
        # ensure to use 3 channels
        image = image[:, :, :3]

        if cvt_color:
            # convert to a openCV2 image, notice the COLOR_RGB2BGR which means that
            # the color is converted from RGB to BGR format
            image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)
        return image

    @staticmethod
    def CVImage_to_PILImage(image):
        color_coverted = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        result = Image.fromarray(color_coverted)
        return result


def create_mp3_response(filename):
    mimetype = 'application/octet-stream'
    response = FileResponse(open(filename, 'rb'), content_type=mimetype)
    response['Content-Disposition'] = f'inline; filename="{filename}"'
    return response
    # response['Content-Disposition'] = 'attachment; filename=filename.mp3'


def index(request):
    return HttpResponse("Hello, world. You're at the index.")


def makeText(request, image_path):
    response = f'You are requesting result of {image_path}'
    return HttpResponse(response)


def eval_settings(key, default):
    result = default
    try:
        result = eval(f'settings.{key}')
    except:
        pass
    return result


def get_number_keywords():
    """
    Get number keywords in settings

    @return:
    """
    return eval_settings('MAX_NUMBER_KEYWORD', 10)


def get_number_objects():
    """
    Get number objects in settings

    @return:
    """
    return eval_settings('MAX_NUMBER_OBJECT', 3)


def get_number_sentences():
    """
    Get number sentences in settings

    @return:
    """
    return eval_settings('MAX_NUMBER_SENTENCE', 3)


def slice_sentence_retrieval_result(result):
    """
    Slice sentence retrieval result, i.e., limit the numbers of objects, keywords, and sentences

    @param result: {}
    @return:
    """
    if not result:
        return result

    no = get_number_objects()
    nk = get_number_keywords()
    ns = get_number_sentences()

    key = list(result.keys())[0]

    for k, n in zip([KO, KK, KS], [no, nk, ns]):
        if k in result[key]:
            result[key][k] = result[key][k][:n]

    return result


def make_sentences(filename):
    """
    Generate sentence from image

    result is like the followings,
    {
        "water": {
            "objects": [
                "water"
            ],
            "sentences": [
                "Could you double bag the water?"
            ],
            "keywords": [
                "waters",
                "bag",
                "put",
                "could",
                "price",
                "sold",
                "separate",
                "packs",
                "sale"
            ]
        }
    }
    water is detected object, double and right, etc., are additional keywords.

    for 2 objects, the root key is x2,
    for more than 2 objects, the root key is xn

    Sentences are ordered by weight.
    """
    objects_dict, _ = ObjectDetection.run_with_seg(filename, draw_image=settings.VERBOSE)

    if settings.VERBOSE:
        secho('Object detection result:', fg='red')
        secho(objects_dict, fg='green')

    result = {}
    if objects_dict:
        # sort objects by confidence desc order
        objects = list(map(lambda x: x[0], sorted(objects_dict.items(), key=lambda x: x[1], reverse=True)))
        # map name, tomato_sauce to tomato sauce
        # objects = [item.replace('_', ' ') for item in objects]
        result = sentence_retrieval.get_sentence_by_objects(objs=objects, keywords=[])
        # slice result
        result = slice_sentence_retrieval_result(result)
    else:
        # failed to detect object
        result = sentence_retrieval.get_sentence_by_objects(objs=None, keywords=[])

    return result


def generate_sentence_sound_file_name(image_basename, detected_object, keyword, index, suffix='wav'):
    """
    Generate sentence sound file name

    @param image_basename: image basename
    @param detected_object: detected object name in image
    @param keyword: keyword
    @param index: sentence index
    @param suffix: sound file type, TTS default is wav file, after converting, the type is ogg
    @return: str, e.g., 1ee14688-61d2-11ec-95a6-9e64c55a142e-water-bag-0.wav
    """
    # fix object name like tomato sauce, remove the space
    detected_object = detected_object.replace(' ', '')

    folder = settings.TTS_SOUND_FOLDER
    name = f'{image_basename}-{detected_object}-{keyword}-{index}.{suffix}'
    return os.path.join(folder, name)


def generate_sound_file_name(basename, suffix='wav'):
    """
    Generate sentence ssound file name only by basename
    @param basename: 
    @param suffix: 
    @return: 
    """
    folder = settings.TTS_SOUND_FOLDER
    name = f'{basename}-{time.time()}.{suffix}'
    return os.path.join(folder, name)


def read_file_base64(filename):
    """
    Read file and encode with base64
    @param filename: filename
    @return: data
    """
    return ImageHelper.encode(filename)


def prepare_response(basename, sr_sentences, voice, rate, volume, overwrite_objects=True):
    """
    Prepare response

    @param image_filename: image full path filename
    @param sr_sentences: the result of make_sentences(image_filename)
    @param voice: male or female
    @param rate: voice parameter
    @param volume: another voice parameter
    @param overwrite_objects: whether to overwrite objects in UI
    @return: dict
    """
    ogg_filenames = []
    ogg_data = []

    # there is one key in sentences, e.g., water, milk, x2, xn
    detected_object = list(sr_sentences.keys())[0]

    objects = sr_sentences[detected_object][KO]
    keywords = sr_sentences[detected_object][KK]
    sentences = sr_sentences[detected_object][KS]

    for idx, item in enumerate(sentences):
        # make file name first
        sound_filename = generate_sentence_sound_file_name(basename, detected_object, '', idx)
        TextToSpeech.run(item, sound_filename, rate, volume, voice)
        # to ogg file to get small size file
        ogg_filename = TextToSpeech.to_ogg_file(sound_filename, keep_original_file=False)
        # get ogg file data
        data = read_file_base64(ogg_filename).decode('ascii')
        ogg_data.append(data)
        # append to ogg_filenames, only using basename
        ogg_filenames.append(os.path.basename(ogg_filename))
    # make up result
    sr_sentences.update(dict(ogg_filenames=ogg_filenames, ogg_data=ogg_data, overwrite_objects=overwrite_objects))

    if settings.VERBOSE:
        secho('+++++++++++++++++++++++', fg='red')
        secho('Base Name', fg='yellow')
        secho(basename, fg='green')
        secho('objects', fg='yellow')
        secho(objects, fg='green')
        secho('keywords', fg='yellow')
        secho(json.dumps(keywords, ensure_ascii=False, indent=2), fg='green')
        secho('sentences', fg='yellow')
        secho(json.dumps(sentences, ensure_ascii=False, indent=2), fg='green')
        secho('ogg_filenames', fg='yellow')
        secho(json.dumps(ogg_filenames, ensure_ascii=False, indent=2), fg='green')
        secho('-----------------------\n\n', fg='red')

    # make json response
    return JsonResponse(sr_sentences)


@csrf_exempt
def makeSound(request):
    # data in base64 format
    data = request.POST['data']
    filename = request.POST['filename']
    voice = request.POST['voice']  # 'male' or 'female'
    rate = int(request.POST['rate'])  # int
    volume = float(request.POST['volume'])  # [0.0, 1.0]
    # cast voice
    voice = cast_voice(voice)

    image_filename = make_image_filename(filename)
    # get PIL image
    # image = ImageHelper.PILImage_to_CVImage(ImageHelper.decode(data, save_to=image_filename, resize=True))
    # save to file
    ImageHelper.decode(data, save_to=image_filename, resize=True)
    sentences = make_sentences(image_filename)

    basename = os.path.splitext(os.path.basename(image_filename))[0]

    if sentences:
        response = prepare_response(basename, sentences, voice, rate, volume, overwrite_objects=True)
        return response
    else:
        return HttpResponseBadRequest('Failed to detect objects')


@csrf_exempt
def makeSentences(request):
    print(request.POST)
    obs = request.POST['object']  # '', or string split by ,

    if not obs:
        obs = None
    else:
        obs = list(set(obs.split(',')))  # remove duplicates

    basename = request.POST["basename"]
    keywords = request.POST['keywords']  # '', or joined with ','
    if keywords:
        keywords = keywords.split(',')
    else:
        keywords = []

    # same with the above
    voice = request.POST['voice']  # 'male' or 'female'
    rate = int(request.POST['rate'])  # int
    volume = float(request.POST['volume'])  # [0.0, 1.0]
    # cast voice
    voice = cast_voice(voice)

    sentences = sentence_retrieval.get_sentence_by_objects(objs=obs, keywords=keywords)
    # slice result
    sentences = slice_sentence_retrieval_result(sentences)

    if sentences:
        response = prepare_response(basename, sentences, voice, rate, volume, overwrite_objects=False)
        return response
    else:
        return HttpResponseBadRequest('Failed to make sentences')


@csrf_exempt
def updateFrequency(request):
    print(request.POST)
    sentence = request.POST['sentence']
    sentence_retrieval.update_frequency(sentence)

    return HttpResponse("OK")
