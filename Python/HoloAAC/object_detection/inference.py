import os
import webbrowser

import cv2
import numpy as np
import torch
from matplotlib import pyplot as plt

import sys

sys.path.append(os.path.dirname(os.path.dirname(__file__)))
# add PaddleSeg deps
from PaddleSeg.deploy.python.holo_process import HoloProcess

# add yolov5 deps
YOLOV5_ROOT = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'yolov5')
sys.path.insert(0, YOLOV5_ROOT)

from models.common import DetectMultiBackend
from utils.augmentations import letterbox
from utils.general import (check_img_size, non_max_suppression, scale_coords)
from utils.plots import Annotator, colors
from utils.torch_utils import select_device

pwd = os.path.dirname(os.path.abspath(__file__))

holo_process = HoloProcess()

# supported object classes
# candy, cereal, chips, chocolate, coffee, corn, fish, flour, jam, milk, pasta, soda, spices, tea, and water

SUPPORTED_OJBECTS = ['candy', 'cereal', 'chips', 'chocolate', 'coffee',
                     'corn', 'fish', 'flour', 'jam', 'milk',
                     'soda', 'spices', 'water']


def yolov5_load_warmup(root, model_path, device='0'):
    weights_path = os.path.join(root, model_path)
    # Load model
    device = select_device(device)
    model = DetectMultiBackend(weights_path, device=device)
    stride, names, pt, jit, onnx, engine = model.stride, model.names, model.pt, model.jit, model.onnx, model.engine

    # Half
    half = False
    half &= (pt or jit or onnx or engine) and device.type != 'cpu'  # FP16 supported on limited backends with CUDA
    if pt or jit:
        model.model.half() if half else model.model.float()

    # warm up
    model.warmup(half=half)
    return model, device, half


# init model and warm up
model_path = 'runs/train/exp3/weights/best.pt'
YOLO_MODEL, DEVICE, HALF = yolov5_load_warmup(YOLOV5_ROOT, model_path)


class ObjectDetection(object):

    def __init__(self, model, device, half, standalone=False):
        self.model = model
        self.device = device
        self.half = half

        self.labels = []
        self.net = None

        self.standalone = standalone

    def get_path_basename(self, filename):
        path = os.path.dirname(filename)
        base_name = os.path.basename(filename)
        name, _ = os.path.splitext(base_name)
        return path, name

    def seg_result_filename(self, filename):
        path, name = self.get_path_basename(filename)
        name = f'{name}-seg.png'
        return os.path.join(path, name)

    def od_result_filename(self, filename):
        path, name = self.get_path_basename(filename)
        name = f'{name}-od.png'
        return os.path.join(path, name)

    def od_raw_result_filename(self, filename):
        path, name = self.get_path_basename(filename)
        name = f'{name}-raw.png'
        return os.path.join(path, name)

    def draw_image(self, image, boxes, ids, labels, confidences, offset_x=0, offset_y=0):
        image = np.ascontiguousarray(image, dtype=np.uint8)
        for idx, i in enumerate(ids):
            # extract the bounding box coordinates
            (x, y) = (boxes[i][0], boxes[i][1])
            (w, h) = (boxes[i][2], boxes[i][3])

            x += offset_x
            y += offset_y

            # draw a bounding box rectangle and label on the image
            color = (0, 0, 255)
            cv2.rectangle(image, (x, y), (x + w, y + h), color, 2)
            text = "{}".format(labels[idx], confidences[idx])
            cv2.putText(image, text, (x + 15, y + 20), cv2.FONT_HERSHEY_SIMPLEX,
                        1, color, 2)

        return image

    def display_img(self, image, filename=None):
        if self.standalone is None:
            return

        if self.standalone:
            plt.close('all')
            fig = plt.figure(figsize=(12, 12))
            plt.axis(False)
            ax = fig.add_subplot(111)
            ax.imshow(image)
            plt.show(block=False)
        else:
            # save to file
            if filename is None:
                filename = 'image_for_show.png'
            cv2.imwrite(filename, image)
            webbrowser.open(filename)

    def display_seg_img(self, filename):
        if os.path.exists(filename):
            webbrowser.open(filename)

    def __call__(self, image, *args, draw_image=False, offset_x=0, offset_y=0, image_for_draw=None, detected={},
                 **kwargs):

        model = self.model
        device = self.device

        half = self.half
        stride = model.stride
        pt = model.pt
        names = model.names

        imgsz = [640, 640]
        conf_thres = 0.50
        iou_thres = 0.45
        classes = None
        agnostic_nms = False
        max_det = 1000

        imgsz = check_img_size(imgsz, s=stride)  # check image size

        # Padded resize
        img = letterbox(image, imgsz, stride=stride, auto=pt)[0]

        # Convert
        img = img.transpose((2, 0, 1))[::-1]  # HWC to CHW, BGR to RGB
        img = np.ascontiguousarray(img)

        im = torch.from_numpy(img).to(device)
        im = im.half() if half else im.float()  # uint8 to fp16/32
        im /= 255  # 0 - 255 to 0.0 - 1.0
        if len(im.shape) == 3:
            im = im[None]  # expand for batch dim
        # Inference
        augment = False
        pred = model(im, augment=augment, visualize=False)

        # NMS
        pred = non_max_suppression(pred, conf_thres, iou_thres, classes, agnostic_nms, max_det=max_det)

        if image_for_draw is None:
            im0 = image.copy()
        else:
            im0 = image_for_draw

        hide_conf = False
        hide_labels = False

        confidences = []
        labels = []

        line_thickness = 2

        # min conf
        min_conf = None
        if 'min_conf' in kwargs:
            min_conf = kwargs['min_conf']

        # draw text on upper side or bottom side to avoid occlusion
        upper = True

        if 'upper' in kwargs:
            upper = kwargs['upper']
        # Process predictions
        for i, det in enumerate(pred):  # per image
            annotator = Annotator(im0, line_width=line_thickness, example=str(names))
            if len(det):
                # Rescale boxes from img_size to im0 size
                det[:, :4] = scale_coords(im.shape[2:], det[:, :4], image.shape).round()

                # Write results
                for *xyxy, conf, cls in reversed(det):

                    if min_conf is not None and conf < min_conf:
                        continue

                    # add offset
                    xyxy[0] += offset_x
                    xyxy[1] += offset_y
                    xyxy[2] += offset_x
                    xyxy[3] += offset_y
                    # Add bbox to image
                    c = int(cls)  # integer class

                    # remove duplicates
                    if detected and names[c] in detected:
                        continue

                    # only care supported objects
                    if SUPPORTED_OJBECTS and names[c] not in SUPPORTED_OJBECTS:
                        continue

                    label = None if hide_labels else (names[c] if hide_conf else f'{names[c]} {conf:.2f}')
                    annotator.box_label(xyxy, label, color=colors(c, True))

                    confidences.append(f'{conf:.2f}')
                    labels.append(names[c])

        result = dict(zip(labels, confidences))
        return result, im0

    @staticmethod
    def run(filename, draw_image=True, standalone=False):
        image = cv2.imread(filename)
        od = ObjectDetection(YOLO_MODEL, DEVICE, HALF, standalone=standalone)
        result, drawn_image = od(image, draw_image=draw_image)
        if drawn_image is not None:
            od_image_filename = od.od_raw_result_filename(filename)
            od.display_img(drawn_image, filename=od_image_filename)
        return result, drawn_image

    @staticmethod
    def run_with_seg(filename, draw_image=True, standalone=False,
                     show_seg_result=True, show_od_result=True):
        # for draw image
        image_for_draw = cv2.imread(filename)
        od = ObjectDetection(YOLO_MODEL, DEVICE, HALF, standalone=standalone)

        # get seg result
        result = {}
        drawn_image = None
        od_image_filename = od.od_result_filename(filename)
        seg_image_filename = od.seg_result_filename(filename)
        # position to draw label
        upper = True
        for image, x, y, w, h in holo_process.clip(filename, write_seg_result_to=seg_image_filename):
            temp, drawn_image = od(image, draw_image=draw_image, offset_x=x, offset_y=y, image_for_draw=image_for_draw,
                                   detected=result, min_conf=0.10, upper=upper)
            # upper = not upper

            result.update(temp)

        if show_seg_result:
            od.display_seg_img(seg_image_filename)

        if show_od_result:
            od.display_img(image_for_draw, filename=od_image_filename)

        return result, drawn_image


if __name__ == '__main__':
    image_path = r"D:\Projects\Code\HoloAACServer\HoloAAC\images\IMG_20220401_085149.png"  # failure
    image_path = r"D:\Projects\Code\HoloAACServer\HoloAAC\images\IMG_20220401_090324.png"  # wrong

    folder = r"D:\OneDrive - George Mason University - O365 Production\Documents\Project\HoloAAC\revise\video\images"
    # folder = r"D:\OneDrive - George Mason University - O365 Production\Documents\Paper\HoloAAC\1st_img"
    folder = r"D:\OneDrive - George Mason University - O365 Production\Documents\Project\HoloAAC\revise\images"
    folder = r"D:\OneDrive - George Mason University - O365 Production\Documents\Paper\HoloAAC\video\revise"
    for img in [
        # r"20220706_100433_HoloLens.00_00_07_04.Still009.png",  # water
        # r"20220706_100433_HoloLens.00_00_57_09.Still001.png",  # coffee, chocolate, water
        # r"20220706_100433_HoloLens.00_01_34_08.Still002.png",  # coffee, soda, water, rice
        # r"20220706_100433_HoloLens.00_02_00_29.Still003.png",  # water, coffee
        # r"20220706_100433_HoloLens.00_02_16_05.Still004.png",  # soda, water
        # r"20220706_100433_HoloLens.00_02_34_00.Still005.png",  # soda, coffee, water
        # r"20220706_100433_HoloLens.00_02_57_21.Still006.png",  # chocolate
        # r"20220706_100433_HoloLens.00_03_27_14.Still007.png",  # +water, soda, chocolate, rice
        # r"20220706_100433_HoloLens.00_05_12_17.Still009.png",  # chocolate, soda, water, rice
        # r"20220706_100433_HoloLens.00_01_34_07.Still010.png",  # coffee, soda, water, rice
        # r"20220706_100433_HoloLens.00_05_09_26.Still012.png"
        # r"g1.png",
        # r"g2.png",
        # r"g3.png",
        # r"g4.png",
        # r"g5.png",
        # r"g6.png",
        # r"g7.png",
        # r"g8.png",
        r"water.png"
    ]:
        image_path = os.path.join(folder, img)

        assert os.path.exists(image_path), f'{image_path} not exist'

        ObjectDetection.run(image_path)

        ObjectDetection.run_with_seg(image_path)
