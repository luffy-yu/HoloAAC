from django.contrib import admin
from django.urls import include, path

from . import views

urlpatterns = [
    path('', views.index, name='index'),
    path('maketext/<image_path>', views.makeText, name='MakeText'),
    path('makesound', views.makeSound, name='makeSound'),
    path('makesentences', views.makeSentences, name='makeSentences'),
    path('updatefrequency', views.updateFrequency, name='updateFrequency'),
]
