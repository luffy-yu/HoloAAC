import json
import os.path

import requests
import base64

host = 'http://127.0.0.1:8000'


def encode_image(filename):
    with open(filename, 'rb') as f:
        return base64.b64encode(f.read())


def test_makesound():
    url = '/webservices/makesound'

    image = 'temp.png'
    data = encode_image(filename=image)
    voice = 'male'  # / female
    rate = 125  # integer
    volume = 0.5  # [0.0,1.0]

    data = dict(data=data, voice=voice, rate=rate, volume=volume, filename='temp.png')

    response = requests.post(f'{host}{url}', data=data)
    assert response.status_code == 200
    print(json.dumps(response.content.decode()))


def test_makesentences():
    url = '/webservices/makesentences'

    basename = '1ee14688-61d2-11ec-95a6-9e64c55a142e'
    _object = 'water'
    keywords = 'bag'
    voice = 'male'  # / female
    rate = 125  # integer
    volume = 0.5  # [0.0,1.0]

    data = dict(basename=basename, object=_object, keywords=keywords, voice=voice, rate=rate, volume=volume)
    response = requests.post(f'{host}{url}', data=data)
    assert response.status_code == 200


if __name__ == '__main__':
    test_makesound()
    test_makesentences()
